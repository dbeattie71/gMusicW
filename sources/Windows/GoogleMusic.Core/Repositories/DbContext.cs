﻿// --------------------------------------------------------------------------------------------------------------------
// OutcoldSolutions (http://outcoldsolutions.com)
// --------------------------------------------------------------------------------------------------------------------

namespace OutcoldSolutions.GoogleMusic.Repositories
{
    using System;
    using System.Globalization;
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
        private const int CurrentDatabaseVersion = 8;
        private readonly string dbFileName;

        public DbContext(string dbFileName = "db.sqlite")
        {
            if (dbFileName == null)
            {
                throw new ArgumentNullException("dbFileName");
            }

            if (Path.IsPathRooted(dbFileName))
            {
                throw new ArgumentException(
                    "Path to database cannot be rooted. It should be relative path to base (local) folder.",
                    "dbFileName");
            }
            else
            {
                this.dbFileName = dbFileName;
            }
        }

        public SQLiteAsyncConnection CreateConnection()
        {
            return new SQLiteAsyncConnection(this.GetDatabaseFilePath(), openFlags: SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.SharedCache | SQLiteOpenFlags.NoMutex, storeDateTimeAsTicks: true);
        }

        public async Task<bool> CheckVersionAsync()
        {
            bool fDbExists = false;
#if NETFX_CORE
            fDbExists = (await ApplicationData.Current.LocalFolder.GetFilesAsync())
                .Any(f => string.Equals(f.Name, this.dbFileName));
#else
            fDbExists = File.Exists(this.GetDatabaseFilePath());
#endif
            SQLite3.Config(SQLite3.ConfigOption.MultiThread);

            if (fDbExists)
            {
                SQLiteAsyncConnection connection = this.CreateConnection();
                int currentVersion = await connection.ExecuteScalarAsync<int>("PRAGMA user_version");

                if (currentVersion == CurrentDatabaseVersion)
                {
                    return true;
                }
            }

            return false;
        }

        public async Task InitializeAsync()
        {
            bool fDbExists = false;

#if NETFX_CORE
            fDbExists = (await ApplicationData.Current.LocalFolder.GetFilesAsync())
                .Any(f => string.Equals(f.Name, this.dbFileName));
#else
            fDbExists = File.Exists(this.GetDatabaseFilePath());
#endif

            SQLiteAsyncConnection connection = null;
            if (fDbExists)
            {
                connection = this.CreateConnection();
                if (connection != null)
                {
                    connection.Close();
                    await this.DeleteDatabaseAsync();
                }
            }

            connection = this.CreateConnection();
            SQLite3.Config(SQLite3.ConfigOption.MultiThread);

            await this.CreateBasicObjectsAsync(connection);

            await this.CreateTriggersAsync(connection);
            await connection.ExecuteAsync(string.Format(CultureInfo.InvariantCulture, "PRAGMA user_version = {0} ;", CurrentDatabaseVersion));
        }

        public async Task DeleteDatabaseAsync()
        {
            var connection = this.CreateConnection();
            if (connection != null)
            {
                connection.Close();
            }

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

        private async Task CreateBasicObjectsAsync(SQLiteAsyncConnection connection)
        {
            await connection.ExecuteAsync("PRAGMA page_size = 65536 ;");

            await connection.CreateTableAsync<Song>();
            await connection.CreateTableAsync<UserPlaylist>();
            await connection.CreateTableAsync<UserPlaylistEntry>();
            await connection.CreateTableAsync<Album>();
            await connection.CreateTableAsync<Genre>();
            await connection.CreateTableAsync<Artist>();
            await connection.CreateTableAsync<CachedSong>();
            await connection.CreateTableAsync<CachedAlbumArt>();
            await connection.CreateTableAsync<Radio>();

            await connection.ExecuteAsync(@"CREATE TABLE [Enumerator] (Id integer primary key autoincrement not null);");
            await connection.ExecuteAsync(@"INSERT INTO [Enumerator] DEFAULT VALUES;");
        }

        private async Task CreateTriggersAsync(SQLiteAsyncConnection connection)
        {
            await connection.ExecuteAsync(@"
CREATE TRIGGER insert_song AFTER INSERT ON [Song]
  BEGIN

    update [Genre]
    set 
        [SongsCount] = [Genre].[SongsCount] + 1,
        [Duration] = [Genre].[Duration] + new.[Duration],
        [ArtUrl] = coalesce( nullif([Genre].[ArtUrl], ''), new.[AlbumArtUrl] ),
        [Recent] = max( new.[Recent],  [Genre].[Recent] )
    where [Genre].TitleNorm = new.GenreTitleNorm and new.IsLibrary = 1;
  
    insert into Genre([Title], [TitleNorm], [SongsCount], [Duration], [ArtUrl], [Recent], OfflineSongsCount, OfflineDuration)
    select new.GenreTitle, new.GenreTitleNorm, 1, new.Duration, new.AlbumArtUrl, new.Recent, 0, 0
    from [Enumerator] as e
         left join [Genre] as g on g.TitleNorm = new.GenreTitleNorm
    where e.[Id] = 1 and g.TitleNorm is null and new.IsLibrary = 1;

    update [Artist]
    set 
        [SongsCount] = [Artist].[SongsCount] + 1,
        [Duration] = [Artist].[Duration] + new.[Duration],
        [ArtUrl] = coalesce( nullif([Artist].[ArtUrl], ''), new.[ArtistArtUrl] ),
        [Recent] = max( new.[Recent],  [Artist].[Recent] ),   
        [GoogleArtistId] = coalesce( nullif([Artist].[GoogleArtistId], ''), new.[GoogleArtistId] )
    where [Artist].TitleNorm = coalesce(nullif(new.AlbumArtistTitleNorm, ''), new.[ArtistTitleNorm]) and new.IsLibrary = 1;

    update [Artist]
    set 
        [SongsCount] = [Artist].[SongsCount] + 1,
        [Duration] = [Artist].[Duration] + new.[Duration],
        [ArtUrl] = coalesce( nullif([Artist].[ArtUrl], ''), new.[ArtistArtUrl] ), 
        [Recent] = max( new.[Recent],  [Artist].[Recent] ),  
        [GoogleArtistId] = coalesce( nullif([Artist].[GoogleArtistId], ''), new.[GoogleArtistId] )
    where [Artist].TitleNorm = new.[ArtistTitleNorm] and new.AlbumArtistTitleNorm <> ''  and new.[ArtistTitleNorm] <> new.AlbumArtistTitleNorm and new.IsLibrary = 1;

    insert into Artist([Title], [TitleNorm], [SongsCount], [Duration], [ArtUrl], [Recent], [AlbumsCount], OfflineSongsCount, OfflineDuration, OfflineAlbumsCount, GoogleArtistId)
    select coalesce(nullif(new.AlbumArtistTitle, ''), new.[ArtistTitle]), coalesce(nullif(new.AlbumArtistTitleNorm, ''), new.[ArtistTitleNorm]), 1, new.Duration, new.ArtistArtUrl, new.Recent, 0, 0, 0, 0, new.GoogleArtistId
    from [Enumerator] as e
         left join [Artist] as a on a.TitleNorm = coalesce(nullif(new.AlbumArtistTitleNorm, ''), new.[ArtistTitleNorm])
    where e.[Id] = 1 and a.TitleNorm is null and new.IsLibrary = 1;

    insert into Artist([Title], [TitleNorm], [SongsCount], [Duration], [ArtUrl], [Recent], [AlbumsCount], OfflineSongsCount, OfflineDuration, OfflineAlbumsCount, GoogleArtistId)
    select new.[ArtistTitle], new.[ArtistTitleNorm], 1, new.Duration, new.AlbumArtUrl, new.Recent, 0, 0, 0, 0, new.GoogleArtistId
    from [Enumerator] as e
         left join [Artist] as a on a.TitleNorm = new.[ArtistTitleNorm]
    where e.[Id] = 1 and new.AlbumArtistTitleNorm <> ''  and new.[ArtistTitleNorm] <> new.AlbumArtistTitleNorm and a.TitleNorm is null and new.IsLibrary = 1;

    update [Album]
    set 
        [SongsCount] = [Album].[SongsCount] + 1,
        [Duration] = [Album].[Duration] + new.[Duration],
        [ArtUrl] = coalesce( nullif([Album].[ArtUrl], ''), new.[AlbumArtUrl] ), 
        [Recent] = max( new.[Recent],  [Album].[Recent] ),  
        [Year] =  nullif(coalesce( nullif([Album].[Year], 0), new.[Year] ), 0),  
        [GenreTitleNorm] =  coalesce( nullif([Album].[GenreTitleNorm], ''), new.[GenreTitleNorm] ),   
        [GoogleAlbumId] =  coalesce( nullif([Album].[GoogleAlbumId], ''), new.[GoogleAlbumId] )  
    where [Album].ArtistTitleNorm = coalesce(nullif(new.AlbumArtistTitleNorm, ''), new.[ArtistTitleNorm]) and TitleNorm = new.AlbumTitleNorm and new.IsLibrary = 1;

    update [Artist]
    set [ArtUrl] = new.[ArtistArtUrl]
    where [Artist].[AlbumsCount] = 0 and [Artist].TitleNorm = coalesce(nullif(new.AlbumArtistTitleNorm, ''), new.[ArtistTitleNorm]) and not exists (select * from [Album] as a where a.ArtistTitleNorm = coalesce(nullif(new.AlbumArtistTitleNorm, ''), new.[ArtistTitleNorm]) and a.TitleNorm = new.AlbumTitleNorm) and new.IsLibrary = 1;

    insert into Album([Title], [TitleNorm], [SongsCount], [Duration], [ArtUrl], [Recent], [Year], [ArtistTitleNorm], [GenreTitleNorm], OfflineSongsCount, OfflineDuration, GoogleAlbumId)
    select new.AlbumTitle, new.AlbumTitleNorm, 1, new.Duration, new.AlbumArtUrl, new.Recent, nullif(new.Year, 0), coalesce(nullif(new.AlbumArtistTitleNorm, ''), new.[ArtistTitleNorm]), new.[GenreTitleNorm], 0, 0, new.GoogleAlbumId
    from [Enumerator] as e
         left join [Album] as a on a.ArtistTitleNorm = coalesce(nullif(new.AlbumArtistTitleNorm, ''), new.[ArtistTitleNorm]) and a.TitleNorm = new.AlbumTitleNorm
    where e.[Id] = 1 and a.TitleNorm is null and new.IsLibrary = 1;

  END;");

            await connection.ExecuteAsync(@"
CREATE TRIGGER delete_song AFTER DELETE ON [Song] 
  BEGIN

    update [Genre]    
    set 
        [SongsCount] = [Genre].[SongsCount] - 1,
        [Duration] = [Genre].[Duration] - old.[Duration],
        [ArtUrl] = (select max(s.[AlbumArtUrl]) from [Song] s where s.[GenreTitleNorm] = old.[GenreTitleNorm]),
        [Recent] = (select max(s.[Recent]) from [Song] s where s.[GenreTitleNorm] = old.[GenreTitleNorm]),
        [OfflineSongsCount] = [Genre].[OfflineSongsCount] - coalesce( (select 1 from CachedSong as cs where cs.SongId = old.SongId) , 0),
        [OfflineDuration] = [Genre].[OfflineSongsCount] - coalesce( (select old.[Duration] from CachedSong as cs where cs.SongId = old.SongId) , 0)      
    where [Genre].TitleNorm = old.GenreTitleNorm and old.IsLibrary = 1;

    delete from [Genre] where [SongsCount] <= 0;

    update [Album]    
    set 
        [SongsCount] = [Album].[SongsCount] - 1,
        [Duration] = [Album].[Duration] - old.[Duration],
        [ArtUrl] = (select max(s.[AlbumArtUrl]) from [Song] s where s.[AlbumTitleNorm] = old.[AlbumTitleNorm]),
        [Recent] = (select max(s.[Recent]) from [Song] s where s.[AlbumTitleNorm] = old.[AlbumTitleNorm]),
        [Year] = (select max(s.[Year]) from [Song] s where s.[AlbumTitleNorm] = old.[AlbumTitleNorm]),
        [GenreTitleNorm] = (select max(s.[GenreTitleNorm]) from [Song] s where s.[AlbumTitleNorm] = old.[AlbumTitleNorm]),
        [OfflineSongsCount] = [Album].[OfflineSongsCount] - coalesce( (select 1 from CachedSong as cs where cs.SongId = old.SongId) , 0),
        [OfflineDuration] = [Album].[OfflineSongsCount] - coalesce( (select old.[Duration] from CachedSong as cs where cs.SongId = old.SongId) , 0) ,
        GoogleAlbumId =  (select max(s.[GoogleAlbumId]) from [Song] s where s.[AlbumTitleNorm] = old.[AlbumTitleNorm])
    where [Album].TitleNorm = old.AlbumTitleNorm and ArtistTitleNorm = coalesce(nullif(old.AlbumArtistTitleNorm, ''), old.[ArtistTitleNorm]) and old.IsLibrary = 1;    

    delete from [Album] where [SongsCount] <= 0;

    update [Artist]
    set 
        [SongsCount] = [Artist].[SongsCount] - 1,
        [Duration] = [Artist].[Duration] - old.[Duration],
        [ArtUrl] = (select max(s.[ArtistArtUrl]) from [Song] s where coalesce(nullif(s.AlbumArtistTitleNorm, ''), s.[ArtistTitleNorm]) = coalesce(nullif(old.AlbumArtistTitleNorm, ''), old.[ArtistTitleNorm])),
        [Recent] = (select max(s.[Recent]) from [Song] s where coalesce(nullif(s.AlbumArtistTitleNorm, ''), s.[ArtistTitleNorm]) = coalesce(nullif(old.AlbumArtistTitleNorm, ''), old.[ArtistTitleNorm])),        
        [OfflineSongsCount] = [Artist].[OfflineSongsCount] - coalesce( (select 1 from CachedSong as cs where cs.SongId = old.SongId) , 0),
        [OfflineDuration] = [Artist].[OfflineSongsCount] - coalesce( (select old.[Duration] from CachedSong as cs where cs.SongId = old.SongId) , 0)   ,
        GoogleArtistId =  (select max(s.[GoogleAlbumId]) from [Song] s where coalesce(nullif(s.AlbumArtistTitleNorm, ''), s.[ArtistTitleNorm]) = coalesce(nullif(old.AlbumArtistTitleNorm, ''), old.[ArtistTitleNorm]))
    where TitleNorm = coalesce(nullif(old.AlbumArtistTitleNorm, ''), old.[ArtistTitleNorm]) and old.IsLibrary = 1;    

    update [Artist]    
    set 
        [SongsCount] = [SongsCount] - 1,
        [Duration] = [Duration] - old.[Duration],
        [ArtUrl] = (select max(s.[ArtistArtUrl]) from [Song] s where coalesce(nullif(s.AlbumArtistTitleNorm, ''), s.[ArtistTitleNorm]) = old.[ArtistTitleNorm]),
        [Recent] = (select max(s.[Recent]) from [Song] s where coalesce(nullif(s.AlbumArtistTitleNorm, ''), s.[ArtistTitleNorm]) = old.[ArtistTitleNorm]),
        [OfflineSongsCount] = [Artist].[OfflineSongsCount] - coalesce( (select 1 from CachedSong as cs where cs.SongId = old.SongId) , 0),
        [OfflineDuration] = [Artist].[OfflineSongsCount] - coalesce( (select old.[Duration] from CachedSong as cs where cs.SongId = old.SongId) , 0)  ,
        GoogleArtistId =  (select max(s.[GoogleAlbumId]) from [Song] s where coalesce(nullif(s.AlbumArtistTitleNorm, ''), s.[ArtistTitleNorm]) = old.[ArtistTitleNorm])
    where [Artist].TitleNorm = old.[ArtistTitleNorm] and old.[AlbumArtistTitleNorm] <> old.[ArtistTitleNorm] and old.IsLibrary = 1;
    
    delete from [Artist] where [SongsCount] <= 0;
  END;
");

            await connection.ExecuteAsync(@"
CREATE TRIGGER insert_userplaylistentry AFTER INSERT ON [UserPlaylistEntry]
  BEGIN
  
    update [UserPlaylist]
    set 
        [SongsCount] = [SongsCount] + 1,
        [Duration] = [UserPlaylist].[Duration] + (select s.[Duration] from [Song] as s where s.[SongId] = new.[SongId]),
        [ArtUrl] = coalesce( nullif([UserPlaylist].[ArtUrl], '') , (select s.[AlbumArtUrl] from [Song] as s where s.[SongId] = new.[SongId]) ),
        [Recent] = max( [UserPlaylist].[Recent],  (select s.[Recent] from [Song] as s where s.[SongId] = new.[SongId]) ) ,
        [OfflineSongsCount] = [UserPlaylist].[OfflineSongsCount] + coalesce( (select 1 from CachedSong cs where new.[SongId] = cs.[SongId]) , 0),        
        [OfflineDuration] = [UserPlaylist].[OfflineDuration] + coalesce( (select s.[Duration] from [Song] as s inner join [CachedSong] as cs on s.SongId = cs.SongId where s.[SongId] = new.[SongId]), 0)
    where [PlaylistId] = new.PlaylistId;

  END;
");

            await connection.ExecuteAsync(@"
CREATE TRIGGER delete_userplaylistentry AFTER DELETE ON [UserPlaylistEntry]
  BEGIN
  
    update [UserPlaylist]
    set 
        [SongsCount] = [SongsCount] - 1,
        [Duration] = [Duration] - (select s.[Duration] from [Song] as s where s.[SongId] = old.[SongId]),
        [ArtUrl] = (select max(s.[AlbumArtUrl]) from [Song] s inner join [UserPlaylistEntry] e on s.SongId = e.SongId where e.PlaylistId = old.PlaylistID),
        [Recent] = coalesce((select max(s.[Recent]) from [Song] s inner join [UserPlaylistEntry] e on s.SongId = e.SongId where e.PlaylistId = old.PlaylistID), 0),
        [OfflineSongsCount] = [UserPlaylist].[OfflineSongsCount] - coalesce( (select 1 from CachedSong cs where old.[SongId] = cs.[SongId]) , 0),        
        [OfflineDuration] = [UserPlaylist].[OfflineDuration] - coalesce( (select s.[Duration] from [Song] as s inner join [CachedSong] as cs on s.SongId = cs.SongId where s.[SongId] = old.[SongId]), 0) 
    where [PlaylistId] = old.PlaylistId;

  END; 
");

            await connection.ExecuteAsync(@"
CREATE TRIGGER update_song_lastplayed AFTER UPDATE OF [Recent] ON [Song]
  BEGIN
  
    update [UserPlaylist]
    set [Recent] = new.[Recent] 
    where old.[Recent] <> new.[Recent] and [PlaylistId] in (select distinct e.[PlaylistId] from [UserPlaylistEntry] e where new.[SongId] = e.[SongId]);

    update [Artist]
    set [Recent] = new.[Recent] 
    where old.[Recent] <> new.[Recent] and [TitleNorm] = new.[ArtistTitleNorm] or [TitleNorm] = new.[AlbumArtistTitleNorm];

    update [Album]
    set [Recent] = new.[Recent] 
    where old.[Recent] <> new.[Recent] and [TitleNorm] = new.[AlbumTitleNorm] and [ArtistTitleNorm] = coalesce(nullif(new.AlbumArtistTitleNorm, ''), new.[ArtistTitleNorm]);

    update [Genre]
    set [Recent] = new.[Recent] 
    where old.[Recent] <> new.[Recent] and [TitleNorm] = new.[GenreTitleNorm];

  END;    
");

            await connection.ExecuteAsync(@"
CREATE TRIGGER update_song_albumarturl AFTER UPDATE OF [AlbumArtUrl] ON [Song]
  BEGIN
  
    update [UserPlaylist]
    set [ArtUrl] = new.[AlbumArtUrl] 
    where old.[AlbumArtUrl] <> new.[AlbumArtUrl] and [PlaylistId] in (select distinct e.[PlaylistId] from [UserPlaylistEntry] e where new.[SongId] = e.[SongId]) and new.IsLibrary = 1;

    update [Album]
    set [ArtUrl] = new.[AlbumArtUrl] 
    where old.[AlbumArtUrl] <> new.[AlbumArtUrl] and [TitleNorm] = new.[AlbumTitleNorm] and [ArtistTitleNorm] = coalesce(nullif(new.AlbumArtistTitleNorm, ''), new.[ArtistTitleNorm]) and new.IsLibrary = 1;

    update [Genre]
    set [ArtUrl] = new.[AlbumArtUrl] 
    where old.[AlbumArtUrl] <> new.[AlbumArtUrl] and [TitleNorm] = new.[GenreTitleNorm] and new.IsLibrary = 1;

  END;
");

            await connection.ExecuteAsync(@"
CREATE TRIGGER update_song_artistarturl AFTER UPDATE OF [ArtistArtUrl] ON [Song]
  BEGIN
  
    update [Artist]
    set [ArtUrl] = new.[ArtistArtUrl] 
    where old.[ArtistArtUrl] <> new.[ArtistArtUrl] and [TitleNorm] = new.[ArtistTitleNorm] or [TitleNorm] = new.[AlbumArtistTitleNorm] and new.IsLibrary = 1;

  END;
");

            await connection.ExecuteAsync(@"
CREATE TRIGGER update_song_parenttitlesupdate AFTER UPDATE OF [AlbumTitleNorm], [GenreTitleNorm], [AlbumArtistTitleNorm], [ArtistTitleNorm], [GoogleAlbumId], [GoogleArtistId] ON [Song]
  BEGIN  

    -------------- DELETE -------------------

    update [Genre]    
    set 
        [SongsCount] = [Genre].[SongsCount] - 1,
        [Duration] = [Genre].[Duration] - old.[Duration],
        [ArtUrl] = (select max(s.[AlbumArtUrl]) from [Song] s where s.[GenreTitleNorm] = old.[GenreTitleNorm]),
        [Recent] = (select max(s.[Recent]) from [Song] s where s.[GenreTitleNorm] = old.[GenreTitleNorm]),
        [OfflineSongsCount] = [Genre].[OfflineSongsCount] - coalesce( (select 1 from CachedSong as cs where cs.SongId = old.SongId) , 0),
        [OfflineDuration] = [Genre].[OfflineSongsCount] - coalesce( (select old.[Duration] from CachedSong as cs where cs.SongId = old.SongId) , 0)          
    where (new.[GenreTitleNorm] <> old.[GenreTitleNorm] or (old.IsLibrary = 1 and new.IsLibrary = 0)) and [Genre].TitleNorm = old.GenreTitleNorm;

    delete from [Genre] where [SongsCount] <= 0;

    update [Album]    
    set 
        [SongsCount] = [Album].[SongsCount] - 1,
        [Duration] = [Album].[Duration] - old.[Duration],
        [ArtUrl] = (select max(s.[AlbumArtUrl]) from [Song] s where s.[AlbumTitleNorm] = old.[AlbumTitleNorm]),
        [Recent] = (select max(s.[Recent]) from [Song] s where s.[AlbumTitleNorm] = old.[AlbumTitleNorm]),
        [Year] = (select max(s.[Year]) from [Song] s where s.[AlbumTitleNorm] = old.[AlbumTitleNorm]),
        [GenreTitleNorm] = (select max(s.[GenreTitleNorm]) from [Song] s where s.[AlbumTitleNorm] = old.[AlbumTitleNorm]),        
        [OfflineSongsCount] = [Album].[OfflineSongsCount] - coalesce( (select 1 from CachedSong as cs where cs.SongId = old.SongId) , 0),
        [OfflineDuration] = [Album].[OfflineSongsCount] - coalesce( (select old.[Duration] from CachedSong as cs where cs.SongId = old.SongId) , 0),
        [GoogleAlbumId] = (select max(s.[GoogleAlbumId]) from [Song] s where s.[AlbumTitleNorm] = old.[AlbumTitleNorm])
    where (new.[AlbumTitleNorm] <> old.[AlbumTitleNorm] or new.GoogleAlbumId <> old.GoogleAlbumId or new.[ArtistTitleNorm] <> old.[ArtistTitleNorm] or new.[AlbumArtistTitleNorm] <> old.[AlbumArtistTitleNorm] or (old.IsLibrary = 1 and new.IsLibrary = 0))
          and [Album].TitleNorm = old.AlbumTitleNorm and [Album].ArtistTitleNorm = coalesce(nullif(old.AlbumArtistTitleNorm, ''), old.[ArtistTitleNorm]);    

    delete from [Album] where [SongsCount] <= 0;

    update [Artist]
    set 
        [SongsCount] = [Artist].[SongsCount] - 1,
        [Duration] = [Artist].[Duration] - old.[Duration],
        [ArtUrl] = (select max(s.[ArtistArtUrl]) from [Song] s where coalesce(nullif(s.AlbumArtistTitleNorm, ''), s.[ArtistTitleNorm]) = coalesce(nullif(old.AlbumArtistTitleNorm, ''), old.[ArtistTitleNorm])),
        [Recent] = (select max(s.[Recent]) from [Song] s where coalesce(nullif(s.AlbumArtistTitleNorm, ''), s.[ArtistTitleNorm]) = coalesce(nullif(old.AlbumArtistTitleNorm, ''), old.[ArtistTitleNorm])),        
        [OfflineSongsCount] = [Artist].[OfflineSongsCount] - coalesce( (select 1 from CachedSong as cs where cs.SongId = old.SongId) , 0),
        [OfflineDuration] = [Artist].[OfflineSongsCount] - coalesce( (select old.[Duration] from CachedSong as cs where cs.SongId = old.SongId) , 0),
        [GoogleArtistId] = (select max(s.[GoogleArtistId]) from [Song] s where coalesce(nullif(s.AlbumArtistTitleNorm, ''), s.[ArtistTitleNorm]) = coalesce(nullif(old.AlbumArtistTitleNorm, ''), old.[ArtistTitleNorm]))
    where (new.[AlbumTitleNorm] <> old.[AlbumTitleNorm] or new.GoogleArtistId <> old.GoogleArtistId or new.[ArtistTitleNorm] <> old.[ArtistTitleNorm] or new.[AlbumArtistTitleNorm] <> old.[AlbumArtistTitleNorm] or (old.IsLibrary = 1 and new.IsLibrary = 0))
          and [Artist].TitleNorm = coalesce(nullif(old.AlbumArtistTitleNorm, ''), old.[ArtistTitleNorm]);    

    update [Artist]    
    set 
        [SongsCount] = [Artist].[SongsCount] - 1,
        [Duration] = [Artist].[Duration] - old.[Duration],
        [ArtUrl] = (select max(s.[ArtistArtUrl]) from [Song] s where coalesce(nullif(s.AlbumArtistTitleNorm, ''), s.[ArtistTitleNorm]) = old.[ArtistTitleNorm]),
        [Recent] = (select max(s.[Recent]) from [Song] s where coalesce(nullif(s.AlbumArtistTitleNorm, ''), s.[ArtistTitleNorm]) = old.[ArtistTitleNorm]),        
        [OfflineSongsCount] = [Artist].[OfflineSongsCount] - coalesce( (select 1 from CachedSong as cs where cs.SongId = old.SongId) , 0),
        [OfflineDuration] = [Artist].[OfflineSongsCount] - coalesce( (select old.[Duration] from CachedSong as cs where cs.SongId = old.SongId) , 0),
        [GoogleArtistId] = (select max(s.[GoogleArtistId]) from [Song] s where coalesce(nullif(s.AlbumArtistTitleNorm, ''), s.[ArtistTitleNorm]) = old.[ArtistTitleNorm])
    where (new.[AlbumTitleNorm] <> old.[AlbumTitleNorm] or new.GoogleArtistId <> old.GoogleArtistId or new.[ArtistTitleNorm] <> old.[ArtistTitleNorm] or new.[AlbumArtistTitleNorm] <> old.[AlbumArtistTitleNorm] or (old.IsLibrary = 1 and new.IsLibrary = 0))
          and [Artist].TitleNorm = old.[ArtistTitleNorm] and old.[AlbumArtistTitleNorm] <> old.[ArtistTitleNorm];
    
    delete from [Artist] where [SongsCount] <= 0;

    -------------- INSERT -------------------
    
    update [Genre]
    set 
        [SongsCount] = [Genre].[SongsCount] + 1,
        [Duration] = [Genre].[Duration] + new.[Duration],
        [ArtUrl] = coalesce( nullif([Genre].[ArtUrl], '') , new.[AlbumArtUrl] ),
        [Recent] = max( new.[Recent], [Genre].[Recent]),        
        [OfflineSongsCount] = [Genre].[OfflineSongsCount] + coalesce( (select 1 from CachedSong as cs where cs.SongId = new.SongId) , 0),
        [OfflineDuration] = [Genre].[OfflineSongsCount] + coalesce( (select new.[Duration] from CachedSong as cs where cs.SongId = new.SongId) , 0)        
    where (new.[GenreTitleNorm] <> old.[GenreTitleNorm] or (old.IsLibrary = 0 and new.IsLibrary = 1)) and [Genre].TitleNorm = new.GenreTitleNorm;
  
    insert into Genre([Title], [TitleNorm], [SongsCount], [Duration], [ArtUrl], [Recent], [OfflineSongsCount], [OfflineDuration])
    select new.GenreTitle, new.GenreTitleNorm, 1, new.Duration, new.AlbumArtUrl, new.Recent, 
      coalesce( (select 1 from CachedSong as cs where cs.SongId = new.SongId) , 0), 
      coalesce( (select new.[Duration] from CachedSong as cs where cs.SongId = new.SongId) , 0)
    from [Enumerator] as e
         left join [Genre] as g on g.TitleNorm = new.GenreTitleNorm
    where (new.[GenreTitleNorm] <> old.[GenreTitleNorm] or (old.IsLibrary = 0 and new.IsLibrary = 1)) and e.[Id] = 1 and g.TitleNorm is null;

    update [Artist]
    set 
        [SongsCount] = [Artist].[SongsCount] + 1,
        [Duration] = [Artist].[Duration] + new.[Duration],
        [ArtUrl] = coalesce( nullif([Artist].[ArtUrl], '') ,  new.[ArtistArtUrl] ),
        [Recent] = max( new.[Recent], [Artist].[Recent] ),        
        [OfflineSongsCount] = [Artist].[OfflineSongsCount] + coalesce( (select 1 from CachedSong as cs where cs.SongId = new.SongId) , 0),
        [OfflineDuration] = [Artist].[OfflineSongsCount] + coalesce( (select new.[Duration] from CachedSong as cs where cs.SongId = new.SongId) , 0),
        [GoogleArtistId] = coalesce( nullif([Artist].[GoogleArtistId], '') , new.[GoogleArtistId] )
    where (new.[AlbumTitleNorm] <> old.[AlbumTitleNorm] or new.GoogleArtistId <> old.GoogleArtistId or new.[ArtistTitleNorm] <> old.[ArtistTitleNorm] or new.[AlbumArtistTitleNorm] <> old.[AlbumArtistTitleNorm] or (old.IsLibrary = 0 and new.IsLibrary = 1))
          and [Artist].TitleNorm = coalesce(nullif(new.AlbumArtistTitleNorm, ''), new.[ArtistTitleNorm]);

    update [Artist]
    set 
        [SongsCount] = [Artist].[SongsCount] + 1,
        [Duration] = [Artist].[Duration] + new.[Duration],
        [ArtUrl] = coalesce(  nullif([Artist].[ArtUrl], '') , new.[ArtistArtUrl] ),
        [Recent] = max( new.[Recent] , [Artist].[Recent] ),        
        [OfflineSongsCount] = [Artist].[OfflineSongsCount] + coalesce( (select 1 from CachedSong as cs where cs.SongId = new.SongId) , 0),
        [OfflineDuration] = [Artist].[OfflineSongsCount] + coalesce( (select new.[Duration] from CachedSong as cs where cs.SongId = new.SongId) , 0),
        [GoogleArtistId] = coalesce(  nullif([Artist].[GoogleArtistId], '') , new.[GoogleArtistId] ) 
    where (new.[AlbumTitleNorm] <> old.[AlbumTitleNorm] or new.GoogleArtistId <> old.GoogleArtistId or new.[ArtistTitleNorm] <> old.[ArtistTitleNorm] or new.[AlbumArtistTitleNorm] <> old.[AlbumArtistTitleNorm] or (old.IsLibrary = 0 and new.IsLibrary = 1))
          and [Artist].TitleNorm = new.[ArtistTitleNorm] and new.AlbumArtistTitleNorm <> ''  and new.[ArtistTitleNorm] <> new.AlbumArtistTitleNorm;

    insert into Artist([Title], [TitleNorm], [SongsCount], [Duration], [ArtUrl], [Recent], [AlbumsCount], [OfflineSongsCount], [OfflineDuration], OfflineAlbumsCount, GoogleArtistId)
    select coalesce(nullif(new.AlbumArtistTitle, ''), new.[ArtistTitle]), coalesce(nullif(new.AlbumArtistTitleNorm, ''), new.[ArtistTitleNorm]), 1, new.Duration, new.ArtistArtUrl, new.Recent, 0, 
      coalesce( (select 1 from CachedSong as cs where cs.SongId = new.SongId) , 0), 
      coalesce( (select new.[Duration] from CachedSong as cs where cs.SongId = new.SongId) , 0),
      coalesce( (select count(*) from [Album] where [ArtistTitleNorm] = coalesce(nullif(new.AlbumArtistTitle, ''), new.[ArtistTitle]) and [OfflineSongsCount] > 0) , 0),
      new.GoogleArtistId
    from [Enumerator] as e
         left join [Artist] as a on a.TitleNorm = coalesce(nullif(new.AlbumArtistTitleNorm, ''), new.[ArtistTitleNorm])
    where (new.[AlbumTitleNorm] <> old.[AlbumTitleNorm] or new.GoogleArtistId <> old.GoogleArtistId or new.[ArtistTitleNorm] <> old.[ArtistTitleNorm] or new.[AlbumArtistTitleNorm] <> old.[AlbumArtistTitleNorm] or (old.IsLibrary = 0 and new.IsLibrary = 1))
          and e.[Id] = 1 and a.TitleNorm is null;

    insert into Artist([Title], [TitleNorm], [SongsCount], [Duration], [ArtUrl], [Recent], [AlbumsCount], [OfflineSongsCount], [OfflineDuration], OfflineAlbumsCount, GoogleArtistId)
    select new.[ArtistTitle], new.[ArtistTitleNorm], 1, new.Duration, new.ArtistArtUrl, new.Recent, 0, 
      coalesce( (select 1 from CachedSong as cs where cs.SongId = new.SongId) , 0), 
      coalesce( (select new.[Duration] from CachedSong as cs where cs.SongId = new.SongId) , 0),
      coalesce( (select count(*) from [Album] where [ArtistTitleNorm] = new.[ArtistTitleNorm] and [OfflineSongsCount] > 0) , 0),
      new.GoogleArtistId
    from [Enumerator] as e
         left join [Artist] as a on a.TitleNorm = new.[ArtistTitleNorm]
    where (new.[AlbumTitleNorm] <> old.[AlbumTitleNorm] or new.GoogleArtistId <> old.GoogleArtistId or new.[ArtistTitleNorm] <> old.[ArtistTitleNorm] or new.[AlbumArtistTitleNorm] <> old.[AlbumArtistTitleNorm] or (old.IsLibrary = 0 and new.IsLibrary = 1))
          and e.[Id] = 1 and new.AlbumArtistTitleNorm <> ''  and new.[ArtistTitleNorm] <> new.AlbumArtistTitleNorm and a.TitleNorm is null;

    update [Album]
    set 
        [SongsCount] = [Album].[SongsCount] + 1,
        [Duration] = [Album].[Duration] + new.[Duration],
        [ArtUrl] = coalesce( nullif([Album].[ArtUrl], '') , new.[AlbumArtUrl] ),
        [Recent] = max( new.[Recent] , [Album].[Recent] ),
        [Year] = coalesce( nullif([Album].[Year], 0) , nullif(new.Year, 0) ),
        [GenreTitleNorm] = coalesce( nullif([Album].[GenreTitleNorm], ''), new.[GenreTitleNorm] ),        
        [OfflineSongsCount] = [Album].[OfflineSongsCount] + coalesce( (select 1 from CachedSong as cs where cs.SongId = new.SongId) , 0),
        [OfflineDuration] = [Album].[OfflineSongsCount] + coalesce( (select new.[Duration] from CachedSong as cs where cs.SongId = new.SongId) , 0),
        GoogleAlbumId =  case when nullif([Album].[GoogleAlbumId], '') is null then new.[GoogleAlbumId] else [Album].[GoogleAlbumId] end
    where (new.[AlbumTitleNorm] <> old.[AlbumTitleNorm] or new.GoogleAlbumId <> old.GoogleAlbumId or new.[ArtistTitleNorm] <> old.[ArtistTitleNorm] or new.[AlbumArtistTitleNorm] <> old.[AlbumArtistTitleNorm] or (old.IsLibrary = 0 and new.IsLibrary = 1))
          and [Album].ArtistTitleNorm = coalesce(nullif(new.AlbumArtistTitleNorm, ''), new.[ArtistTitleNorm]) and TitleNorm = new.AlbumTitleNorm;

    update [Artist]
    set [ArtUrl] = new.[ArtistArtUrl]
    where [Artist].[AlbumsCount] = 0 and (new.[AlbumTitleNorm] <> old.[AlbumTitleNorm] or new.GoogleArtistId <> old.GoogleArtistId or new.[ArtistTitleNorm] <> old.[ArtistTitleNorm] or new.[AlbumArtistTitleNorm] <> old.[AlbumArtistTitleNorm] or (old.IsLibrary = 0 and new.IsLibrary = 1))
          and [Artist].TitleNorm = coalesce(nullif(new.AlbumArtistTitleNorm, ''), new.[ArtistTitleNorm]) and not exists (select * from [Album] as a where a.ArtistTitleNorm = coalesce(nullif(new.AlbumArtistTitleNorm, ''), new.[ArtistTitleNorm]) and a.TitleNorm = new.AlbumTitleNorm);

    insert into Album([Title], [TitleNorm], [SongsCount], [Duration], [ArtUrl], [Recent], [Year], [ArtistTitleNorm], [GenreTitleNorm], [OfflineSongsCount], [OfflineDuration], GoogleAlbumId)
    select new.AlbumTitle, new.AlbumTitleNorm, 1, new.Duration, new.AlbumArtUrl, new.Recent, nullif(new.Year, 0), coalesce(nullif(new.AlbumArtistTitleNorm, ''), new.[ArtistTitleNorm]), new.[GenreTitleNorm], 
      coalesce( (select 1 from CachedSong as cs where cs.SongId = new.SongId) , 0), 
      coalesce( (select new.[Duration] from CachedSong as cs where cs.SongId = new.SongId) , 0),
      new.GoogleAlbumId
    from [Enumerator] as e
         left join [Album] as a on a.ArtistTitleNorm = coalesce(nullif(new.AlbumArtistTitleNorm, ''), new.[ArtistTitleNorm]) and a.TitleNorm = new.AlbumTitleNorm
    where (new.[AlbumTitleNorm] <> old.[AlbumTitleNorm] or new.GoogleAlbumId <> old.GoogleAlbumId or new.[ArtistTitleNorm] <> old.[ArtistTitleNorm] or new.[AlbumArtistTitleNorm] <> old.[AlbumArtistTitleNorm] or (old.IsLibrary = 0 and new.IsLibrary = 1))
          and e.[Id] = 1 and a.TitleNorm is null;

  END; 
");

            await connection.ExecuteAsync(@"CREATE TRIGGER insert_cachedsong AFTER INSERT ON CachedSong
  BEGIN  
        
    update [Genre]    
    set     
      [OfflineSongsCount] = [Genre].[OfflineSongsCount] + 1,
      [OfflineDuration] = [Genre].[OfflineDuration] + (select s.Duration from Song s where s.SongId = new.SongId)
    where nullif(new.FileName, '') is not null and [Genre].TitleNorm = (select s.GenreTitleNorm from Song s where s.SongId = new.SongId and s.IsLibrary = 1);    

    update [Artist]    
    set     
      [OfflineSongsCount] = [Artist].[OfflineSongsCount] + 1,
      [OfflineDuration] = [Artist].[OfflineDuration] + (select s.Duration from Song s where s.SongId = new.SongId)  
    where nullif(new.FileName, '') is not null and [Artist].TitleNorm = (select coalesce(nullif(s.AlbumArtistTitleNorm, ''), s.[ArtistTitleNorm]) from Song s where s.SongId = new.SongId and s.IsLibrary = 1);

    update [Artist]    
    set     
      [OfflineSongsCount] = [Artist].[OfflineSongsCount] + 1,
      [OfflineDuration] = [Artist].[OfflineDuration] + (select s.Duration from Song s where s.SongId = new.SongId)  
    where nullif(new.FileName, '') is not null and [Artist].TitleNorm = (select s.[ArtistTitleNorm] from Song s where s.SongId = new.SongId and s.AlbumArtistTitleNorm <> ''  and s.[ArtistTitleNorm] <> s.AlbumArtistTitleNorm and s.IsLibrary = 1);    

    update [Album]    
    set     
      [OfflineSongsCount] = [Album].[OfflineSongsCount] + 1,
      [OfflineDuration] = [Album].[OfflineDuration] + (select s.Duration from Song s where s.SongId = new.SongId)  
    where nullif(new.FileName, '') is not null and exists (select * from Song s where s.SongId = new.SongId and [Album].TitleNorm = s.[AlbumTitleNorm] and [Album].[ArtistTitleNorm] == coalesce(nullif(s.AlbumArtistTitleNorm, ''), s.[ArtistTitleNorm]) and s.IsLibrary = 1);

    update [UserPlaylist]    
    set     
      [OfflineSongsCount] = [UserPlaylist].[OfflineSongsCount] + (select count(*) from UserPlaylistEntry as e where e.PlaylistId = [UserPlaylist].[PlaylistId] and e.SongId = new.SongId),
      [OfflineDuration] = [UserPlaylist].[OfflineDuration] + (select sum(s.Duration) from Song s inner join UserPlaylistEntry as e on e.SongId = s.[SongId] and e.PlaylistId = [UserPlaylist].[PlaylistId] where s.SongId = new.SongId)  
    where nullif(new.FileName, '') is not null and exists (select * from UserPlaylistEntry as e where e.SongId = new.SongId and e.PlaylistId = [UserPlaylist].[PlaylistId]);

    update [Song]    
    set     
       [IsCached] = 1       
    where nullif(new.FileName, '') is not null and [Song].SongId = new.SongId;

  END;");

            await connection.ExecuteAsync(@"CREATE TRIGGER delete_cachedsong AFTER DELETE ON CachedSong
  BEGIN  
        
    update [Genre]    
    set     
      [OfflineSongsCount] = [Genre].[OfflineSongsCount] - 1,
      [OfflineDuration] = [Genre].[OfflineDuration] - (select s.Duration from Song s where s.SongId = old.SongId)
    where nullif(old.FileName, '') is not null and [Genre].TitleNorm = (select s.GenreTitleNorm from Song s where s.SongId = old.SongId and s.IsLibrary = 1);    

    update [Artist]    
    set     
      [OfflineSongsCount] = [Artist].[OfflineSongsCount] - 1,
      [OfflineDuration] = [Artist].[OfflineDuration] - (select s.Duration from Song s where s.SongId = old.SongId)  
    where nullif(old.FileName, '') is not null and [Artist].TitleNorm = (select coalesce(nullif(s.AlbumArtistTitleNorm, ''), s.[ArtistTitleNorm]) from Song s where s.SongId = old.SongId and s.IsLibrary = 1);

    update [Artist]    
    set     
      [OfflineSongsCount] = [Artist].[OfflineSongsCount] - 1,
      [OfflineDuration] = [Artist].[OfflineDuration] - (select s.Duration from Song s where s.SongId = old.SongId)  
    where nullif(old.FileName, '') is not null and [Artist].TitleNorm = (select s.[ArtistTitleNorm] from Song s where s.SongId = old.SongId and s.AlbumArtistTitleNorm <> ''  and s.[ArtistTitleNorm] <> s.AlbumArtistTitleNorm and s.IsLibrary = 1);    

    update [Album]    
    set     
      [OfflineSongsCount] = [Album].[OfflineSongsCount] - 1,
      [OfflineDuration] = [Album].[OfflineDuration] - (select s.Duration from Song s where s.SongId = old.SongId)  
    where nullif(old.FileName, '') is not null and exists (select * from Song s where s.SongId = old.SongId and [Album].TitleNorm = s.[AlbumTitleNorm] and [Album].[ArtistTitleNorm] == coalesce(nullif(s.AlbumArtistTitleNorm, ''), s.[ArtistTitleNorm]) and s.IsLibrary = 1);

    update [UserPlaylist]    
    set     
      [OfflineSongsCount] = [UserPlaylist].[OfflineSongsCount] - (select count(*) from UserPlaylistEntry as e where e.PlaylistId = [UserPlaylist].[PlaylistId] and e.SongId = old.SongId),
      [OfflineDuration] = [UserPlaylist].[OfflineDuration] - (select sum(s.Duration) from Song s inner join UserPlaylistEntry as e on e.SongId = s.[SongId] and e.PlaylistId = [UserPlaylist].[PlaylistId] where s.SongId = old.SongId)  
    where nullif(old.FileName, '') is not null and exists (select * from UserPlaylistEntry as e where e.SongId = old.SongId and e.PlaylistId = [UserPlaylist].[PlaylistId]);

    update [Song]    
    set     
       [IsCached] = 0     
    where [Song].SongId = old.SongId;

  END;");

            await connection.ExecuteAsync(@"CREATE TRIGGER update_cachedsong AFTER UPDATE ON CachedSong
  BEGIN  
        
    update [Genre]    
    set     
      [OfflineSongsCount] = [Genre].[OfflineSongsCount] + 1,
      [OfflineDuration] = [Genre].[OfflineDuration] + (select s.Duration from Song s where s.SongId = old.SongId)
    where nullif(old.FileName, '') is null and nullif(new.[FileName], '') is not null and new.SongId = old.SongId and [Genre].TitleNorm = (select s.GenreTitleNorm from Song s where s.SongId = old.SongId and s.IsLibrary = 1);    

    update [Artist]    
    set     
      [OfflineSongsCount] = [Artist].[OfflineSongsCount] + 1,
      [OfflineDuration] = [Artist].[OfflineDuration] + (select s.Duration from Song s where s.SongId = old.SongId)  
    where nullif(old.FileName, '') is null and nullif(new.[FileName], '') is not null and new.SongId = old.SongId and [Artist].TitleNorm = (select coalesce(nullif(s.AlbumArtistTitleNorm, ''), s.[ArtistTitleNorm]) from Song s where s.SongId = old.SongId and s.IsLibrary = 1);

    update [Artist]    
    set     
      [OfflineSongsCount] = [Artist].[OfflineSongsCount] + 1,
      [OfflineDuration] = [Artist].[OfflineDuration] + (select s.Duration from Song s where s.SongId = old.SongId)  
    where nullif(old.FileName, '') is null and nullif(new.[FileName], '') is not null and new.SongId = old.SongId and [Artist].TitleNorm = (select s.[ArtistTitleNorm] from Song s where s.SongId = old.SongId and s.AlbumArtistTitleNorm <> ''  and s.[ArtistTitleNorm] <> s.AlbumArtistTitleNorm and s.IsLibrary = 1);    

    update [Album]    
    set     
      [OfflineSongsCount] = [Album].[OfflineSongsCount] + 1,
      [OfflineDuration] = [Album].[OfflineDuration] + (select s.Duration from Song s where s.SongId = old.SongId)  
    where nullif(old.FileName, '') is null and nullif(new.[FileName], '') is not null and new.SongId = old.SongId and exists (select * from Song s where s.SongId = old.SongId and [Album].TitleNorm = s.[AlbumTitleNorm] and [Album].[ArtistTitleNorm] == coalesce(nullif(s.AlbumArtistTitleNorm, ''), s.[ArtistTitleNorm]) and s.IsLibrary = 1);

    update [UserPlaylist]    
    set     
      [OfflineSongsCount] = [UserPlaylist].[OfflineSongsCount] + (select count(*) from UserPlaylistEntry as e where e.PlaylistId = [UserPlaylist].[PlaylistId] and e.SongId = old.SongId),
      [OfflineDuration] = [UserPlaylist].[OfflineDuration] + (select sum(s.Duration) from Song s inner join UserPlaylistEntry as e on e.SongId = s.[SongId] and e.PlaylistId = [UserPlaylist].[PlaylistId] where s.SongId = old.SongId)  
    where nullif(old.FileName, '') is null and nullif(new.[FileName], '') is not null and new.SongId = old.SongId and exists (select * from UserPlaylistEntry as e where e.SongId = old.SongId and e.PlaylistId = [UserPlaylist].[PlaylistId]);

    update [Song]    
    set     
       [IsCached] = 1       
    where nullif(old.FileName, '') is null and nullif(new.[FileName], '') is not null and [Song].SongId = new.SongId;

  END;");

            await connection.ExecuteAsync(@"CREATE TRIGGER insert_album AFTER INSERT ON [Album]
  BEGIN      
                                                    
    update [Artist]    
    set    
      [AlbumsCount] = [Artist].[AlbumsCount] + 1,      
      [OfflineAlbumsCount] = [Artist].[OfflineAlbumsCount] + (case when new.[OfflineSongsCount] > 0 then 1 else 0 end)
    where [Artist].[TitleNorm] = new.[ArtistTitleNorm];

  END;");

            await connection.ExecuteAsync(@"CREATE TRIGGER delete_album AFTER DELETE ON [Album]
  BEGIN      
                                                    
    update [Artist]    
    set    
      [AlbumsCount] = [Artist].[AlbumsCount] - 1,      
      [OfflineAlbumsCount] = [Artist].[OfflineAlbumsCount] - (case when old.[OfflineSongsCount] > 0 then 1 else 0 end)
    where [Artist].[TitleNorm] = old.[ArtistTitleNorm];

  END;");

            await connection.ExecuteAsync(@"CREATE TRIGGER update_album AFTER UPDATE OF [OfflineSongsCount] ON [Album]
  BEGIN                             

    update [Artist]    
    set         
      [OfflineAlbumsCount] = [Artist].[OfflineAlbumsCount] + (case when new.[OfflineSongsCount] > 0 then 1 else 0 end)
    where  old.[OfflineSongsCount] = 0 and new.[OfflineSongsCount] > 0 and [Artist].[TitleNorm] = new.[ArtistTitleNorm];
                                                    
    update [Artist]    
    set       
      [OfflineAlbumsCount] = [Artist].[OfflineAlbumsCount] - (case when old.[OfflineSongsCount] > 0 then 1 else 0 end)
    where old.[OfflineSongsCount] > 0 and new.[OfflineSongsCount] = 0 and [Artist].[TitleNorm] = old.[ArtistTitleNorm];

  END;");
        }

        private string GetDatabaseFilePath()
        {
#if NETFX_CORE
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, this.dbFileName);
#else
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, this.dbFileName);
#endif
        }

        [Table("sqlite_master")]
        public class SqliteMasterRecord
        {
            [Column("name")]
            public string Name { get; set; }
        }
    }
}
