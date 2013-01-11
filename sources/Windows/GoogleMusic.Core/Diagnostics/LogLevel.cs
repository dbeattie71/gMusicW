﻿// --------------------------------------------------------------------------------------------------------------------
// Outcold Solutions (http://outcoldman.com)
// --------------------------------------------------------------------------------------------------------------------

namespace OutcoldSolutions.GoogleMusic.Diagnostics
{
    using System;

    [Flags]
    public enum LogLevel
    {
        None = 0,

        OnlyError = 1,

        Error = OnlyError,

        OnlyWarning = 2,

        Warning = OnlyWarning | Error,

        OnlyDebug = 3,

        Debug = OnlyDebug | Warning,

        OnlyInfo = 4,

        Info = OnlyInfo | Debug,
    }
}