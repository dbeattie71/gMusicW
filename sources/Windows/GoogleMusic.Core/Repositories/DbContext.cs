﻿// --------------------------------------------------------------------------------------------------------------------
// OutcoldSolutions (http://outcoldsolutions.com)
// --------------------------------------------------------------------------------------------------------------------

namespace OutcoldSolutions.GoogleMusic.Repositories
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using OutcoldSolutions.GoogleMusic.Models;

    using SQLite;

#if NETFX_CORE
    using Windows.Storage;
#endif

    public class DbContext
    {
        private readonly string dbFileName;

        public DbContext(string dbFileName = "db.v1.sqlite")
        {
            if (dbFileName == null)
            {
                throw new ArgumentNullException("dbFileName");
            }

            if (Path.IsPathRooted(dbFileName))
            {
                throw new ArgumentException("Path to database cannot be rooted. It should be relative path to base (local) folder.", "dbFileName");
            }
            else
            {
                this.dbFileName = dbFileName;
            }
        }

        public enum DatabaseStatus
        {
            Unknown = 0,
            New = 1,
            Existed = 2
        }

        public SQLiteAsyncConnection CreateConnection()
        {
            return new SQLiteAsyncConnection(this.GetDatabaseFilePath(), storeDateTimeAsTicks: true);
        }

        public async Task<DatabaseStatus> InitializeAsync()
        {
            bool fDbExists = false;

#if NETFX_CORE
            fDbExists = (await ApplicationData.Current.LocalFolder.GetFilesAsync())
                .Any(f => string.Equals(f.Name, this.dbFileName));
#else
            fDbExists = File.Exists(this.GetDatabaseFilePath());
#endif
            if (!fDbExists)
            {
                var connection = this.CreateConnection();
                await connection.CreateTableAsync<SongEntity>();
                await connection.CreateTableAsync<UserPlaylistEntity>();
                await connection.CreateTableAsync<UserPlaylistEntryEntity>();
            }

            return fDbExists ? DatabaseStatus.Existed : DatabaseStatus.New;
        }

        public async Task DeleteDatabaseAsync()
        {
#if NETFX_CORE
            var dbFile = (await ApplicationData.Current.LocalFolder.GetFilesAsync())
                .FirstOrDefault(f => string.Equals(f.Name, this.dbFileName));

            if (dbFile != null)
            {
                await dbFile.DeleteAsync();
            }
#else
            await Task.Run(
                () =>
                    {
                        var databaseFilePath = this.GetDatabaseFilePath();
                        if (File.Exists(databaseFilePath))
                        {
                            File.Delete(databaseFilePath);
                        }
                    });
#endif
        }

        private string GetDatabaseFilePath()
        {
#if NETFX_CORE
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, this.dbFileName);
#else
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, this.dbFileName);
#endif
        }
    }
}