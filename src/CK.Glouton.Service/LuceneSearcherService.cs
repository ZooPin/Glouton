﻿using System;
using System.Collections.Generic;
using System.IO;
using CK.Glouton.Lucene;
using CK.Glouton.Model.Logs;
using Microsoft.Extensions.Options;

namespace CK.Glouton.Service
{
    public class LuceneSearcherService : ILuceneSearcherService
    {
        private readonly LuceneConfiguration _configuration;
        private readonly LuceneSearcherManager _searcherManager;

        public LuceneSearcherService( IOptions<LuceneConfiguration> configuration )
        {
            _configuration = configuration.Value;
            _searcherManager = new LuceneSearcherManager(_configuration);
        }

        public List<ILogViewModel> Search(string query, params string[] appNames )
        {
            LuceneSearcherConfiguration configuration = new LuceneSearcherConfiguration();
            configuration.MaxResult = (uint)_configuration.MaxSearch;
            configuration.Fields = new[] { "LogLevel", "Exception" };
            configuration.AppName = appNames;
            if (query == "*")
            {
                configuration.SearchAll(LuceneWantAll.Log);
                return _searcherManager.GetSearcher(appNames).Search(configuration);
            }

            configuration.SearchMethod = SearchMethod.FullText;
            return _searcherManager.GetSearcher(appNames).Search(configuration);
        }

        public List<ILogViewModel> GetAll(params string[] appNames)
        {
            LuceneSearcherConfiguration configuration = new LuceneSearcherConfiguration();
            configuration.MaxResult = (uint)_configuration.MaxSearch;
            configuration.Fields = new[] { "LogLevel" };
            configuration.AppName = appNames;
            configuration.SearchAll(LuceneWantAll.Log);

            return _searcherManager.GetSearcher(appNames).Search(configuration);
        }

        public List<ILogViewModel> GetLogWithFilters( string monitorId, DateTime start, DateTime end, string[] fields, string[] logLevel, string query,  params string[] appNames)
        {
            LuceneSearcherConfiguration configuration = new LuceneSearcherConfiguration
            {
                MonitorId = monitorId,
                DateStart = start,
                DateEnd = end,
                Fields = fields,
                LogLevel = logLevel,
                Query = query,
                AppName = appNames,
                MaxResult = (uint)_configuration.MaxSearch
            };
            if (configuration.Fields == null)
                configuration.Fields = new[] { "LogLevel" };

            return _searcherManager.GetSearcher(appNames).Search(configuration);
        }

        public List<string> GetMonitorIdList()
        {
            LuceneSearcherConfiguration configuration = new LuceneSearcherConfiguration();
            return _searcherManager.GetSearcher(GetAppNameList().ToArray()).GetAllMonitorID();
        }

        /// <summary>
        /// Return of the App Name indexed by Lucene.
        /// </summary>
        /// <returns></returns>
        public List<string> GetAppNameList()
        {
            var directoryInfo = new DirectoryInfo(_configuration.Path);
            var dirs = new List<string>();

            foreach (var info in directoryInfo.GetDirectories())
                dirs.Add(info.Name);

            return dirs;
        }
    }
}
