﻿namespace CK.Glouton.Model.Lucene.Logs
{
    public interface ILogViewModel
    {
        ELogType LogType { get; }
        IExceptionViewModel Exception { get; set; }
        string LogTime { get; set; }
        string LogLevel { get; set; }
        int GroupDepth { get; set; }

    }
}
