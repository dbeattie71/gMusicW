﻿// --------------------------------------------------------------------------------------------------------------------
// Outcold Solutions (http://outcoldman.com)
// --------------------------------------------------------------------------------------------------------------------
namespace OutcoldSolutions.GoogleMusic.Repositories
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using OutcoldSolutions.Diagnostics;
    using OutcoldSolutions.GoogleMusic.BindingModels;
    using OutcoldSolutions.GoogleMusic.Repositories.DbModels;

    public interface ISongsRepository
    {
        Task<IList<SongBindingModel>> GetAllAsync();

        Task<SongBindingModel> GetSongAsync(string songId);
    }

    public class SongsRepository : RepositoryBase, ISongsRepository
    {
        private readonly ILogger logger;

        public SongsRepository(
            ILogManager logManager)
        {
            this.logger = logManager.CreateLogger("SongsRepository");
        }

        public async Task<SongBindingModel> GetSongAsync(string songId)
        {
            return new SongBindingModel(await this.Connection.GetAsync<Song>(songId));
        }

        public async Task<IList<SongBindingModel>> GetAllAsync()
        {
            return (await this.Connection.Table<Song>().ToListAsync()).Select(x => new SongBindingModel(x)).ToList();
        }
    }
}