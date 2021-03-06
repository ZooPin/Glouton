﻿using CK.Glouton.Lucene;
using CK.Glouton.Model.Lucene.Logs;
using CK.Glouton.Model.Lucene.Logs.Implementation;
using CK.Glouton.Model.Lucene.Searcher;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CK.Glouton.Tests
{
    [TestFixture]
    public class LuceneTests
    {
        [SetUp]
        public void SetUp()
        {
            TestHelper.Setup();
        }

#if NET461
        [TestFixtureSetUp]
#else
        [OneTimeSetUp]
#endif
        public void ConstructIndex()
        {
            LuceneTestIndexBuilder.ConstructIndex();
        }

        private const int LuceneMaxSearch = 10;
        private static readonly string LucenePath = Path.Combine( TestHelper.GetTestLogDirectory(), "Lucene" );

        private static readonly LuceneConfiguration LuceneSearcherConfiguration = new LuceneConfiguration
        {
            MaxSearch = LuceneMaxSearch,
            Path = LucenePath,
            Directory = Assembly.GetExecutingAssembly().GetName().Name
        };

        [Test]
        public void log_can_be_indexed_and_searched_with_full_text_search()
        {


            LuceneSearcherManager searcherManager = new LuceneSearcherManager( LuceneSearcherConfiguration );
            var searcher = searcherManager.GetSearcher( LuceneSearcherConfiguration.Directory );

            LuceneSearcherConfiguration configuration = new LuceneSearcherConfiguration
            {
                Fields = new[] { "LogLevel", "Text" },
                ESearchMethod = ESearchMethod.FullText,
                MaxResult = 10,
                Query = "Text:\"Hello world\""
            };

            var result = searcher.Search( configuration );
            result.Should().NotBeNull();
            result.Count.Should().Be( 1 );
            result[ 0 ].LogType.Should().Be( ELogType.Line );

            var log = result[ 0 ] as LineViewModel;
            log.Text.Should().Be( "Hello world" );
            log.LogLevel.Should().Contain( "Info" );

            configuration.Query = "Text:\"CriticalError\"";

            result = searcher.Search( configuration );
            result.Should().NotBeNull();
            result.Count.Should().Be( 1 );
            result[ 0 ].LogType.Should().Be( ELogType.Line );

            log = result[ 0 ] as LineViewModel;
            log.Text.Should().Be( "CriticalError" );
            log.LogLevel.Should().Contain( "Error" );
        }

        [Test]
        public void log_can_be_indexed_and_searched_with_object_search()
        {
            LuceneSearcherManager searcherManager = new LuceneSearcherManager( LuceneSearcherConfiguration );
            var searcher = searcherManager.GetSearcher( LuceneSearcherConfiguration.Directory );

            LuceneSearcherConfiguration configuration = new LuceneSearcherConfiguration
            {
                Fields = new[] { "Text" },
                ESearchMethod = ESearchMethod.WithConfigurationObject,
                MaxResult = 10,
                Query = "\"Hello world\""
            };

            //
            // Search an all document with `Text` field equal to "Hello world"
            //
            var result = searcher.Search( configuration );
            result.Should().NotBeNull();
            result.Count.Should().Be( 1 );
            result[ 0 ].LogType.Should().Be( ELogType.Line );

            var log = result[ 0 ] as LineViewModel;
            log.Text.Should().Be( "Hello world" );
            log.LogLevel.Should().Contain( "Info" );
            log.Tags.Should().BeOfType<string>();
            log.SourceFileName.Should().BeOfType<string>();
            log.LineNumber.Should().BeOfType<string>();
            log.LogLevel.Should().BeOfType<string>();
            log.MonitorId.Should().BeOfType<string>();
            log.GroupDepth.Should().BeOfType( typeof( int ) );
            log.PreviousEntryType.Should().BeOfType<string>();
            log.PreviousLogTime.Should().BeOfType<string>();
            log.AppName.Should().BeOfType<string>();
            log.LogTime.Should().BeOfType<string>();
            log.Exception.Should().BeNull();

            //
            // Search an all document with `Text` field equal to "CriticalError"
            //
            configuration.Query = "CriticalError";
            result = searcher.Search( configuration );

            result.Should().NotBeNull();
            result.Count.Should().Be( 1 );
            result[ 0 ].LogType.Should().Be( ELogType.Line );

            log = result[ 0 ] as LineViewModel;
            log.Text.Should().Be( "CriticalError" );
            log.LogLevel.Should().Contain( "Error" );



            configuration.SearchAll( ELuceneWantAll.Log );
            result = searcher.Search( configuration );
            result.Count.Should().Be( LuceneTestIndexBuilder.TotalLogCount );

            //
            // Search all document with LogLevel between 0002-01-01 to 9999-01-01
            //
            configuration = new LuceneSearcherConfiguration
            {
                Fields = new string[ 0 ],
                ESearchMethod = ESearchMethod.WithConfigurationObject,
                MaxResult = 10,
                DateStart = new DateTime( 2, 01, 01 ),
                DateEnd = new DateTime( 9999, 01, 01 )
            };
            result = searcher.Search( configuration );
            result.Count.Should().Be( LuceneTestIndexBuilder.TotalLogCount );

            //
            // Search all document with LogLevel between 0002-01-01 to 0003-01-01
            //
            configuration = new LuceneSearcherConfiguration
            {
                ESearchMethod = ESearchMethod.WithConfigurationObject,
                MaxResult = 10,
                DateStart = new DateTime( 2, 01, 01 ),
                DateEnd = new DateTime( 3, 01, 01 )
            };
            result = searcher.Search( configuration );
            result.Count.Should().Be( 0 );


            //
            // Search all MonitorId in all appname contain in the searcher
            //
            var monitorId = searcher.GetAllMonitorId();
            monitorId.Count.Should().Be( 2 );

            //
            // Search with false MonitorId
            //
            configuration = new LuceneSearcherConfiguration
            {
                ESearchMethod = ESearchMethod.WithConfigurationObject,
                MaxResult = 10,
                MonitorId = Guid.NewGuid().ToString()
            };
            result = searcher.Search( configuration );
            result.Count.Should().Be( 0 );

            //
            // Search all fatal log
            //
            configuration = new LuceneSearcherConfiguration
            {
                ESearchMethod = ESearchMethod.WithConfigurationObject,
                MaxResult = 10,
                LogLevel = new string[] { "Fatal" }
            };
            result = searcher.Search( configuration );
            result.Count.Should().Be( 1 );

            //
            // Search all log with GroupDepth 
            //
            configuration = new LuceneSearcherConfiguration
            {
                ESearchMethod = ESearchMethod.WithConfigurationObject,
                MaxResult = 10,
                GroupDepth = 1
            };
            result = searcher.Search( configuration );
            result.All( l => l.GroupDepth == 1 ).Should().BeTrue();

            //
            // Search all document with a LogLevel and a monitorId
            //
            configuration = new LuceneSearcherConfiguration
            {
                ESearchMethod = ESearchMethod.WithConfigurationObject,
                MaxResult = 10,
                Fields = new string[] { LogField.MONITOR_ID }
            };
            result = searcher.Search( configuration );
            result.Count.Should().Be( LuceneTestIndexBuilder.TotalLogCount );
        }

        [Test]
        public void get_searcher_with_bad_appname()
        {
            LuceneSearcherManager searcherManager = new LuceneSearcherManager( LuceneSearcherConfiguration );
            var s = searcherManager.GetSearcher( new[] { Guid.NewGuid().ToString() } );
            s.Should().BeNull();
        }

        [Test]
        public void luceneSearcherManager_return_log_order_by_date()
        {
            LuceneSearcherManager searcherManager = new LuceneSearcherManager( LuceneSearcherConfiguration );
            var searcher = searcherManager.GetSearcher( LuceneSearcherConfiguration.Directory );

            LuceneSearcherConfiguration configuration = new LuceneSearcherConfiguration
            {
                MaxResult = 20
            };

            configuration.SearchAll( ELuceneWantAll.Log );
            var result = searcher.Search( configuration );
            result.SequenceEqual( result.OrderBy( l => l.LogTime ) ).Should().BeTrue();
        }

        [Test]
        public void luceneSearcherManager_return_good_appName()
        {
            LuceneSearcherManager searcherManager = new LuceneSearcherManager( LuceneSearcherConfiguration );
            var fakeName = Guid.NewGuid().ToString();
            string directoryPath = LuceneSearcherConfiguration.Path + "\\" + fakeName;

            Directory.CreateDirectory( directoryPath );

            var appName = searcherManager.AppName;
            appName.Count.Should().NotBe( Directory.GetDirectories( LuceneSearcherConfiguration.Path ).Length );
            appName.Contains( LuceneSearcherConfiguration.Directory ).Should().BeTrue();
            appName.Any( a => a == fakeName ).Should().BeFalse();

            Directory.Delete( directoryPath );
        }

        [Test]
        public void bad_configuration_should_throw_exception()
        {
            LuceneSearcherManager searcherManager = new LuceneSearcherManager( LuceneSearcherConfiguration );
            var searcher = searcherManager.GetSearcher( LuceneSearcherConfiguration.Directory );

            Action action = () => searcher.Search( null );
            action.ShouldThrow<ArgumentNullException>();

            action = () => searcher.Search( new LuceneSearcherConfiguration() );
            action.ShouldThrow<ArgumentException>();

        }

        [Test]
        public void log_with_aggregated_exception_can_be_indexed_and_searched()
        {
            LuceneSearcherManager searcherManager = new LuceneSearcherManager( LuceneSearcherConfiguration );
            var searcher = searcherManager.GetSearcher( LuceneSearcherConfiguration.Directory );

            LuceneSearcherConfiguration configuration = new LuceneSearcherConfiguration
            {
                MaxResult = 10,
            };
            configuration.SearchAll( ELuceneWantAll.Exception );

            var result = searcher.Search( configuration );
            result.Should().NotBeNull();
            result.Count.Should().Be( 1 );
            result[ 0 ].LogType.Should().Be( ELogType.Line );

            var log = result[ 0 ] as LineViewModel;
            log.Exception.Should().NotBeNull();
            log.LogLevel.Should().Contain( "Fatal" );
            log.Exception.Message.Should().Contain( "Aggregate exceptions list" );
            log.Exception.AggregatedExceptions.Should().NotBeNull();
            log.Exception.AggregatedExceptions.Count.Should().Be( 3 );

            foreach( var exception in log.Exception.AggregatedExceptions )
            {
                exception.Message.Should().NotBeNullOrEmpty();
                exception.StackTrace.Should().NotBeNullOrEmpty();
            }
        }

        [Test]
        public void lucene_statistics_should_retrieve_values()
        {
            LuceneStatistics luceneStatistics = new LuceneStatistics( LuceneSearcherConfiguration );
            luceneStatistics.AllExceptionCount.Should().BeGreaterThan( 0 );
            luceneStatistics.AllLogCount.Should().BeGreaterThan( 0 );
            luceneStatistics.AppNameCount.Should().NotBe( -1 );
            luceneStatistics.GetAppNames.Count().Should().BeGreaterThan( 0 );
            luceneStatistics.LogInAppNameCount( "badappaname" ).Should().BeLessThan( 0 );
            luceneStatistics.LogInAppNameCount( "badappaname" ).Should().Be( -1 );
            luceneStatistics.ExceptionInAppNameCount( "badappaname" ).Should().BeLessThan( 0 );
            luceneStatistics.ExceptionInAppNameCount( "badappaname" ).Should().Be( -1 );
        }
    }
}
