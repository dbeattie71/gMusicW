﻿// --------------------------------------------------------------------------------------------------------------------
// Outcold Solutions (http://outcoldman.com)
// --------------------------------------------------------------------------------------------------------------------
namespace OutcoldSolutions.GoogleMusic.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using OutcoldSolutions.GoogleMusic.Models;

    public interface IPlaylistCollection<TPlaylist> where TPlaylist : Playlist 
    {
        Task<int> CountAsync();

        Task<IEnumerable<TPlaylist>> GetAllAsync(Order order = Order.Name, int takeCount = int.MaxValue);

        Task<IEnumerable<TPlaylist>> SearchAsync(string query, int takeCount = int.MaxValue);
    }

    public interface IAlbumCollection : IPlaylistCollection<Album>
    {
    }

    public interface IArtistCollection : IPlaylistCollection<Artist>
    {
        
    }

    public interface IGenreCollection : IPlaylistCollection<Genre>
    {
    }
}