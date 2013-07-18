﻿// --------------------------------------------------------------------------------------------------------------------
// Outcold Solutions (http://outcoldman.com)
// --------------------------------------------------------------------------------------------------------------------
namespace OutcoldSolutions.GoogleMusic.Models
{
    using System;

    [Flags]
    public enum StreamType : byte
    {
        Free = 0,

        EphemeralSubscription = 1,

        OwnLibrary = 2,

        Type6 = 6,

        Type7 = 7
    }
}