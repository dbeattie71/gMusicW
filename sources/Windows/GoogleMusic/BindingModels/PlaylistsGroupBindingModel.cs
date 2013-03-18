﻿// --------------------------------------------------------------------------------------------------------------------
// Outcold Solutions (http://outcoldman.com)
// --------------------------------------------------------------------------------------------------------------------
namespace OutcoldSolutions.GoogleMusic.BindingModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    using OutcoldSolutions.GoogleMusic.Models;
    using OutcoldSolutions.GoogleMusic.Repositories.DbModels;

    public class PlaylistsGroupBindingModel
    {
        public PlaylistsGroupBindingModel(string title, int playlistsCount, IEnumerable<PlaylistBindingModel> playlists, PlaylistsRequest request)
        {
            this.Title = title;
            this.Playlists = new ObservableCollection<PlaylistBindingModel>(playlists);
            this.PlaylistsCount = playlistsCount;
            this.Request = request;
        }

        public PlaylistsGroupBindingModel(string title, int playlistsCount, IEnumerable<PlaylistBindingModel> playlists)
        {
            this.Title = title;
            this.PlaylistsCount = playlistsCount;
            this.Playlists = new ObservableCollection<PlaylistBindingModel>(playlists);
        }

        public string Title { get; private set; }

        public int PlaylistsCount { get; private set; }

        public ObservableCollection<PlaylistBindingModel> Playlists { get; private set; }

        public PlaylistsRequest Request { get; set; }
    }

    public class GroupPlaylistsGroupBindingModel
    {
        public GroupPlaylistsGroupBindingModel(
            string title,
            int itemsCount,
            IList<GroupPlaylistBindingModel> playlists,
            PlaylistsRequest request)
        {
            this.Title = title;
            this.Playlists = playlists;
            this.ItemsCount = itemsCount;
            this.Request = request;
        }

        public GroupPlaylistsGroupBindingModel(
            string title,
            int itemsCount,
            IList<GroupPlaylistBindingModel> playlists)
        {
            this.Title = title;
            this.ItemsCount = itemsCount;
            this.Playlists = playlists;
        }

        public string Title { get; private set; }

        public int ItemsCount { get; private set; }

        public IList<GroupPlaylistBindingModel> Playlists { get; private set; }

        public PlaylistsRequest Request { get; set; }
    }

    public class GroupPlaylistBindingModel
    {
        private readonly ISongsContainer playlist;

        public GroupPlaylistBindingModel(ISongsContainer playlist)
        {
            if (playlist == null)
            {
                throw new ArgumentNullException("playlist");
            }

            this.playlist = playlist;
        }

        public DelegateCommand PlayCommand { get; set; }

        public ISongsContainer Playlist
        {
            get
            {
                return this.playlist;
            }
        }
    }
}