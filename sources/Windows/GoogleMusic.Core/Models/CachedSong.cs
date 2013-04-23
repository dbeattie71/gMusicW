﻿// --------------------------------------------------------------------------------------------------------------------
// OutcoldSolutions (http://outcoldsolutions.com)
// --------------------------------------------------------------------------------------------------------------------
namespace OutcoldSolutions.GoogleMusic.Models
{
    using System;

    using SQLite;

    [Table("CachedSong")]
    public class CachedSong
    {
        [PrimaryKey]
        public int SongId { get; set; }

        public DateTime TaskAdded { get; set; }

        public string Path { get; set; }

        [Reference]
        public Song Song { get; set; }
    }
}