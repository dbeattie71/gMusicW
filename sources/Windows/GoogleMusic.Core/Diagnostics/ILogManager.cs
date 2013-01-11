﻿// --------------------------------------------------------------------------------------------------------------------
// Outcold Solutions (http://outcoldman.com)
// --------------------------------------------------------------------------------------------------------------------

namespace OutcoldSolutions.GoogleMusic.Diagnostics
{
    using System;
    using System.Collections.Concurrent;

    public interface ILogManager
    {
        ConcurrentDictionary<Type, ILogWriter> Writers { get; }

        LogLevel LogLevel { get; set; }

        ILogger CreateLogger(string context);
    }
}