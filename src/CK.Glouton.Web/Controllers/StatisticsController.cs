﻿using CK.Glouton.Model.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace CK.Glouton.Web.Controllers
{
    [Route( "api/stats" )]
    public class StatisticsController : Controller
    {
        private readonly ILuceneStatisticsService _luceneStatistics;
        public StatisticsController( ILuceneStatisticsService luceneStatistics )
        {
            _luceneStatistics = luceneStatistics;
        }

        [HttpGet( "log/total/by/appname" )]
        public Dictionary<string, int> LogPerAppName()
        {
            return _luceneStatistics.GetLogByAppName();
        }

        [HttpGet( "exception/total/by/appname" )]
        public Dictionary<string, int> ExceptionPerAppName()
        {
            return _luceneStatistics.GetExceptionByAppName();
        }

        [HttpGet( "log/total" )]
        public int AllLogCount() => _luceneStatistics.AllLogCount();

        [HttpGet( "appname/total" )]
        public int AppNameCount() => _luceneStatistics.AppNameCount;

        [HttpGet( "exception/total" )]
        public int AllException() => _luceneStatistics.AllExceptionCount;

        [HttpGet( "appnames" )]
        public IEnumerable<string> AppNames() => _luceneStatistics.GetAppNames;
    }
}