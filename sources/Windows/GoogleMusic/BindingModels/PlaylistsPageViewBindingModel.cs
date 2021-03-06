﻿// --------------------------------------------------------------------------------------------------------------------
// OutcoldSolutions (http://outcoldsolutions.com)
// --------------------------------------------------------------------------------------------------------------------
namespace OutcoldSolutions.GoogleMusic.BindingModels
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    using OutcoldSolutions.BindingModels;
    using OutcoldSolutions.GoogleMusic.Models;

    public class PlaylistsPageViewBindingModel : BindingModelBase
    {
        private readonly ObservableCollection<PlaylistBindingModel> selectedItems;
        private IList<PlaylistBindingModel> playlists;
        private IList<PlaylistsGroupBindingModel> groups;

        private string title;
        private PlaylistType playlistType;

        public PlaylistsPageViewBindingModel()
        {
            this.selectedItems = new ObservableCollection<PlaylistBindingModel>();
        }

        public string Title
        {
            get
            {
                return this.title;
            }

            set
            {
                this.SetValue(ref this.title, value);
            }
        }

        public string Subtitle
        {
            get
            {
                return this.Playlists == null ? string.Empty : this.Playlists.Count.ToString();
            }
        }

        public PlaylistType PlaylistType
        {
            get
            {
                return this.playlistType;
            }

            set
            {
                this.SetValue(ref this.playlistType, value);
            }
        }

        public IList<PlaylistBindingModel> Playlists
        {
            get
            {
                return this.playlists;
            }

            set
            {
                if (this.SetValue(ref this.playlists, value))
                {
                    this.RecalculateGroups();
                    this.RaisePropertyChanged(() => this.Subtitle);
                }
            }
        }

        public IList<PlaylistsGroupBindingModel> Groups
        {
            get
            {
                return this.groups;
            }

            set
            {
                this.SetValue(ref this.groups, value);
            }
        }

        public ObservableCollection<PlaylistBindingModel> SelectedItems
        {
            get
            {
                return this.selectedItems;
            }
        }

        public void ClearSelectedItems()
        {
            if (this.selectedItems.Count > 0)
            {
                this.selectedItems.Clear();
            }
        }

        private void RecalculateGroups()
        {
            if (this.Playlists == null)
            {
                this.Groups = null;
            }
            else
            {
                this.Groups = this.Playlists.GroupBy(p => p.Playlist.Title.Length > 0 ? char.ToUpper(p.Playlist.Title[0]) : ' ')
                                                        .Select(g => new PlaylistsGroupBindingModel(g.Key.ToString(), g.Count(), g.ToList()))
                                                        .ToList();
            }
        }
    }
}