﻿using Cake.Common.Build;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Common.Solution;
using Cake.Common.Tools.DotNetCore;
using Cake.Common.Tools.DotNetCore.Build;
using Cake.Common.Tools.DotNetCore.Pack;
using Cake.Common.Tools.DotNetCore.Restore;
using Cake.Common.Tools.NuGet;
using Cake.Common.Tools.NuGet.Push;
using Cake.Common.Tools.NUnit;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Npm;
using Cake.Npm.Install;
using Cake.Npm.RunScript;
using CK.Text;
using SimpleGitVersion;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CodeCake
{
    public static class DotNetCoreRestoreSettingsExtension
    {
        public static T AddVersionArguments<T>(this T @this, SimpleRepositoryInfo info, Action<T> conf = null) where T : DotNetCoreSettings
        {
            if (info.IsValid)
            {
                var prev = @this.ArgumentCustomization;
                @this.ArgumentCustomization = args => (prev?.Invoke(args) ?? args)
                        .Append($@"/p:CakeBuild=""true""")
                        .Append($@"/p:Version=""{info.SafeNuGetVersion}""")
                        .Append($@"/p:AssemblyVersion=""{info.MajorMinor}.0""")
                        .Append($@"/p:FileVersion=""{info.FileVersion}""")
                        .Append($@"/p:InformationalVersion=""{info.SafeSemVersion} ({info.SafeNuGetVersion}) - SHA1: {info.CommitSha} - CommitDate: {info.CommitDateUtc:u}""");
            }
            conf?.Invoke(@this);
            return @this;
        }
    }

    /// <summary>
    /// Standard build "script".
    /// </summary>
    [AddPath("CodeCakeBuilder/Tools")]
    [AddPath("%UserProfile%/.nuget/packages/**/tools*")]
    public class Build : CodeCakeHost
    {
        public Build()
        {
            Cake.Log.Verbosity = Verbosity.Diagnostic;

            const string solutionName = "CK-Glouton";
            const string solutionFileName = "../" + solutionName + ".sln";

            var releasesDir = Cake.Directory("CodeCakeBuilder/Releases");

            var projects = Cake.ParseSolution(solutionFileName)
                           .Projects
                           .Where(p => !(p is SolutionFolder)
                                       && p.Name != "CodeCakeBuilder");

            // We do not publish .Tests projects for this solution.
            var projectsToPublish = projects
                                        .Where(p => !p.Path.Segments.Contains("Tests"));

            SimpleRepositoryInfo gitInfo = Cake.GetSimpleRepositoryInfo();

            // Configuration is either "Debug" or "Release".
            string configuration = "Debug";

            Task("Check-Repository")
                .Does(() =>
                {
                    if (!gitInfo.IsValid)
                    {
                        if (Cake.IsInteractiveMode()
                            && Cake.ReadInteractiveOption("Repository is not ready to be published. Proceed anyway?", 'Y', 'N') == 'Y')
                        {
                            Cake.Warning("GitInfo is not valid, but you choose to continue...");
                        }
                        else if (!Cake.AppVeyor().IsRunningOnAppVeyor)
                            throw new Exception("Repository is not ready to be published.");
                    }

                    if (gitInfo.IsValidRelease
                         && (gitInfo.PreReleaseName.Length == 0 || gitInfo.PreReleaseName == "rc"))
                    {
                        configuration = "Release";
                    }

                    Cake.Information("Publishing {0} projects with version={1} and configuration={2}: {3}",
                        projectsToPublish.Count(),
                        gitInfo.SafeSemVersion,
                        configuration,
                        projectsToPublish.Select(p => p.Name).Concatenate());
                });

            Task("Clean")
                .IsDependentOn("Check-Repository")
                .Does(() =>
                {
                    Cake.CleanDirectories(projects.Select(p => p.Path.GetDirectory().Combine("bin")));
                    Cake.CleanDirectories(releasesDir);
                });


            Task("Restore-NPM-Package")
               .IsDependentOn("Clean")
               .Does(() =>
               {
                   var settings = new NpmInstallSettings();

                   settings.LogLevel = NpmLogLevel.Warn;
                   settings.WorkingDirectory = "../src/CK.Glouton.Web/app";
                   settings.InstallLocally();

                   NpmInstallAliases.NpmInstall(this.Cake, settings);
               });
            Task("NPM-Build")
               .IsDependentOn("Restore-NPM-Package")
               .IsDependentOn("Clean")
               .Does(() =>
               {
                   var settings = new NpmRunScriptSettings();

                   settings.LogLevel = NpmLogLevel.Info;
                   settings.WorkingDirectory = "../src/CK.Glouton.Web/app";
                   settings.ScriptName = "build";

                   NpmRunScriptAliases.NpmRunScript(this.Cake, settings);
               });

            Task("Build-With-Version")
                .IsDependentOn("Check-Repository")
                .IsDependentOn("Clean")
                .IsDependentOn("Restore-NPM-package")
                .IsDependentOn("NPM-Build")
                .Does(() =>
                {
                    using (var tempSln = Cake.CreateTemporarySolutionFile(solutionFileName))
                    {
                        tempSln.ExcludeProjectsFromBuild("CodeCakeBuilder");
                        Cake.DotNetCoreBuild(tempSln.FullPath.FullPath,
                            new DotNetCoreBuildSettings().AddVersionArguments(gitInfo, s =>
                            {
                                s.Configuration = configuration;
                            }));
                    }
                });

            Task("Unit-Testing")
                .IsDependentOn("Build-With-Version")
                .Does(() =>
                {
                    var testProjects = projects.Where(p => p.Path.Segments.Contains("Tests"));

                    var testNetFrameworkDlls = testProjects
                        .Where(p => p.Name.EndsWith(".NetFramework"))
                        .Select(p => p.Path.GetDirectory().CombineWithFilePath("bin/" + configuration + "/net461/" + p.Name + ".dll"));
                    Cake.Information("Testing: {0}", string.Join(", ", testNetFrameworkDlls.Select(p => p.GetFilename().ToString())));
                    Cake.NUnit(testNetFrameworkDlls);

                    var testNetCoreDirectories = testProjects
                        .Where(p => p.Name.EndsWith(".NetCore"))
                        .Select(p => p.Path.GetDirectory());
                    Cake.Information("Testing: {0}", string.Join(", ", testNetCoreDirectories.Select(p => p.GetDirectoryName().ToString())));
                    foreach (var testNetCoreDirectory in testNetCoreDirectories)
                        Cake.DotNetCoreRun(testNetCoreDirectory.FullPath);
                });

            Task("Create-NuGet-Packages")
                .WithCriteria(() => gitInfo.IsValid)
                .IsDependentOn("Unit-Testing")
                .Does(() =>
                {
                    Cake.CreateDirectory(releasesDir);
                    foreach (SolutionProject p in projectsToPublish)
                    {
                        Cake.Warning(p.Path.GetDirectory().FullPath);
                        var s = new DotNetCorePackSettings();
                        s.ArgumentCustomization = args => args.Append("--include-symbols");
                        s.NoBuild = true;
                        s.Configuration = configuration;
                        s.OutputDirectory = releasesDir;
                        s.AddVersionArguments(gitInfo);
                        Cake.DotNetCorePack(p.Path.GetDirectory().FullPath, s);
                    }
                });

            Task("Push-NuGet-Packages")
                .WithCriteria(() => gitInfo.IsValid)
                .IsDependentOn("Create-NuGet-Packages")
                .Does(() =>
                {
                    IEnumerable<FilePath> nugetPackages = Cake.GetFiles(releasesDir.Path + "/*.nupkg");
                    if (Cake.IsInteractiveMode())
                    {
                        var localFeed = Cake.FindDirectoryAbove("LocalFeed");
                        if (localFeed != null)
                        {
                            Cake.Information("LocalFeed directory found: {0}", localFeed);
                            if (Cake.ReadInteractiveOption("Do you want to publish to LocalFeed?", 'Y', 'N') == 'Y')
                            {
                                Cake.CopyFiles(nugetPackages, localFeed);
                            }
                        }
                    }
                    if (gitInfo.IsValidRelease)
                    {
                        // All Version
                        PushNuGetPackages("MYGET_PREVIEW_API_KEY", "https://www.myget.org/F/glouton-preview/api/v2/package", nugetPackages);
                    }
                    else
                    {
                        Debug.Assert(gitInfo.IsValidCIBuild);
                        PushNuGetPackages("MYGET_CI_API_KEY", "https://www.myget.org/F/glouton-ci/api/v2/package", nugetPackages);
                    }
                    if (Cake.AppVeyor().IsRunningOnAppVeyor)
                    {
                        Cake.AppVeyor().UpdateBuildVersion(gitInfo.SafeSemVersion);
                    }
                });

            // The Default task for this script can be set here.
            Task("Default")
                .IsDependentOn("Push-NuGet-Packages");
        }

        void PushNuGetPackages(string apiKeyName, string pushUrl, IEnumerable<FilePath> nugetPackages)
        {
            // Resolves the API key.
            var apiKey = Cake.InteractiveEnvironmentVariable(apiKeyName);
            if (string.IsNullOrEmpty(apiKey))
            {
                Cake.Information("Could not resolve {0}. Push to {1} is skipped.", apiKeyName, pushUrl);
            }
            else
            {
                var settings = new NuGetPushSettings
                {
                    Source = pushUrl,
                    ApiKey = apiKey,
                    Verbosity = NuGetVerbosity.Detailed
                };

                foreach (var nupkg in nugetPackages.Where(p => !p.FullPath.EndsWith(".symbols.nupkg")))
                {
                    Cake.Information($"Pushing '{nupkg}' to '{pushUrl}'.");
                    Cake.NuGetPush(nupkg, settings);
                }
            }
        }
    }
}
