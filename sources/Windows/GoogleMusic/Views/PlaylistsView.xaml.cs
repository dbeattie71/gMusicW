﻿// --------------------------------------------------------------------------------------------------------------------
// Outcold Solutions (http://outcoldman.com)
// --------------------------------------------------------------------------------------------------------------------

namespace OutcoldSolutions.GoogleMusic.Views
{
    using System.Collections.Generic;

    using OutcoldSolutions.GoogleMusic.BindingModels;
    using OutcoldSolutions.GoogleMusic.Models;
    using OutcoldSolutions.GoogleMusic.Presenters;
    using OutcoldSolutions.GoogleMusic.Services;

    using Windows.System;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Automation;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Controls.Primitives;
    using Windows.UI.Xaml.Input;

    public interface IPlaylistsView : IView
    {
        void EditPlaylist(PlaylistBindingModel selectedItem);
    }

    public sealed partial class PlaylistsView : ViewBase, IPlaylistsView
    {
        private readonly Button addPlaylistButton;
        private readonly Button editPlaylistButton;
        private readonly Button deletePlaylistButton;
        private readonly Border separator;

        public PlaylistsView()
        {
            this.InitializePresenter<PlaylistsViewPresenter>();
            this.InitializeComponent();

            this.addPlaylistButton = new Button()
                                         {
                                             Style = (Style)Application.Current.Resources["AddAppBarButtonStyle"],
                                             Command = this.Presenter<PlaylistsViewPresenter>().AddPlaylistCommand
                                         };

            this.editPlaylistButton = new Button()
                                         {
                                             Style = (Style)Application.Current.Resources["EditAppBarButtonStyle"],
                                             Command = this.Presenter<PlaylistsViewPresenter>().EditPlaylistCommand
                                         };
            this.editPlaylistButton.SetValue(AutomationProperties.NameProperty, "Rename");

            this.deletePlaylistButton = new Button()
                                         {
                                             Style = (Style)Application.Current.Resources["DeleteAppBarButtonStyle"],
                                             Command = this.Presenter<PlaylistsViewPresenter>().DeletePlaylistCommand
                                         };

            this.separator = new Border() { Style = (Style)Application.Current.Resources["AppBarSeparator"] };
        }

        public override void OnNavigatedTo(object parameter)
        {
            if (this.ListView.Items != null && this.ListView.Items.Count > 0)
            {
                this.ListView.ScrollIntoView(this.ListView.Items[0]);
            }

            base.OnNavigatedTo(parameter);

            var currentContextCommands = App.Container.Resolve<ICurrentContextCommands>();

            if (parameter is PlaylistsRequest && ((PlaylistsRequest)parameter) == PlaylistsRequest.Playlists)
            {
                this.ListView.SelectionMode = ListViewSelectionMode.Single;
                currentContextCommands.SetCommands(new List<ButtonBase>() { this.addPlaylistButton });
            }
            else
            {
                this.ListView.SelectionMode = ListViewSelectionMode.None;
                currentContextCommands.SetCommands(null);
            }
        }

        public void EditPlaylist(PlaylistBindingModel selectedItem)
        {
            this.PlaylistNamePopup.VerticalOffset = this.ActualHeight - 240;
            this.TextBoxPlaylistName.Text = selectedItem.Playlist.Title;
            this.SaveNameButton.IsEnabled = !string.IsNullOrEmpty(this.TextBoxPlaylistName.Text);
            this.PlaylistNamePopup.IsOpen = true;
            this.TextBoxPlaylistName.Focus(FocusState.Keyboard);
        }

        private void PlaylistItemClick(object sender, ItemClickEventArgs e)
        {
            this.Presenter<PlaylistsViewPresenter>().ItemClick(e.ClickedItem as PlaylistBindingModel);
        }

        private void ListViewOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.Presenter<PlaylistsViewPresenter>().BindingModel.IsEditable)
            {
                var uiElements = new List<UIElement>() { this.addPlaylistButton };

                if (this.ListView.SelectedIndex >= 0)
                {
                    uiElements.Add(this.separator);
                    uiElements.Add(this.editPlaylistButton);
                    uiElements.Add(this.deletePlaylistButton);
                }

                var currentContextCommands = App.Container.Resolve<ICurrentContextCommands>();
                currentContextCommands.SetCommands(uiElements);
            }
        }

        private void SaveNameClick(object sender, RoutedEventArgs e)
        {
            this.Presenter<PlaylistsViewPresenter>().ChangePlaylistName(this.TextBoxPlaylistName.Text);
            this.PlaylistNamePopup.IsOpen = false;
        }

        private void CancelChangeNameClick(object sender, RoutedEventArgs e)
        {
            this.PlaylistNamePopup.IsOpen = false;
        }

        private void TextBoxPlaylistNameKeyUp(object sender, KeyRoutedEventArgs e)
        {
            this.SaveNameButton.IsEnabled = !string.IsNullOrEmpty(this.TextBoxPlaylistName.Text);
        }

        private void TextBoxPlaylistNameOnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                this.SaveNameClick(sender, e);
                e.Handled = true;
            }
        }
    }
}
