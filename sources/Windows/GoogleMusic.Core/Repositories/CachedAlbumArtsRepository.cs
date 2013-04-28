﻿// --------------------------------------------------------------------------------------------------------------------
// OutcoldSolutions (http://outcoldsolutions.com)
// --------------------------------------------------------------------------------------------------------------------
namespace OutcoldSolutions.GoogleMusic.Repositories
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using OutcoldSolutions.GoogleMusic.Models;

    public interface ICachedAlbumArtsRepository
    {
        Task<CachedAlbumArt> FindAsync(Uri path, uint size);

        Task AddAsync(CachedAlbumArt cache);

        Task<IList<CachedAlbumArt>> GetRemovedCachedItemsAsync();

        Task DeleteCachedItemsAsync(IEnumerable<CachedAlbumArt> cachedAlbumArts);

        Task ClearCacheAsync();
    }

    public class CachedAlbumArtsRepository : RepositoryBase, ICachedAlbumArtsRepository
    {
        private const string SqlRemovedCachedItems = @"select distinct c.* 
from CachedAlbumArt c
     left join Song s on s.AlbumArtUrl = c.AlbumArtUrl
 where s.[SongId] is null";

        public Task<CachedAlbumArt> FindAsync(Uri path, uint size)
        {
            return this.Connection.Table<CachedAlbumArt>().Where(c => c.Size == size && c.AlbumArtUrl == path).FirstOrDefaultAsync();
        }

        public Task AddAsync(CachedAlbumArt cache)
        {
            return this.Connection.InsertAsync(cache);
        }

        public async Task<IList<CachedAlbumArt>> GetRemovedCachedItemsAsync()
        {
            return await this.Connection.QueryAsync<CachedAlbumArt>(SqlRemovedCachedItems);
        }

        public Task DeleteCachedItemsAsync(IEnumerable<CachedAlbumArt> cachedAlbumArts)
        {
            return this.Connection.RunInTransactionAsync(
                c =>
                    {
                        foreach (var cachedAlbumArt in cachedAlbumArts)
                        {
                            c.Delete<CachedAlbumArt>(cachedAlbumArt);
                        }
                    });
        }

        public Task ClearCacheAsync()
        {
            return this.Connection.RunInTransactionAsync(c => c.DeleteAll<CachedAlbumArt>());
        }
    }
}