﻿//--------------------------------------------------------------------------------------------------------------------
// Outcold Solutions (http://outcoldman.com)
//--------------------------------------------------------------------------------------------------------------------

namespace OutcoldSolutions.GoogleMusic.Views
{
    using System.Collections.Generic;
    using System.Diagnostics;

    using OutcoldSolutions.GoogleMusic.Models;
    using OutcoldSolutions.GoogleMusic.Presenters;
    using OutcoldSolutions.GoogleMusic.Services;

    using Windows.ApplicationModel.Store;
    using Windows.UI.ViewManagement;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;

    public interface IMediaElemenetContainerView : IView
    {
        MediaElement GetMediaElement();

        void Activate();
    }

    public interface IMainView : IView, IMediaElemenetContainerView
    {
        void ShowView(IView view);

        void HideView();
    }

    public sealed partial class MainView : PageBase, IMainView, IMediaElemenetContainerView, ICurrentContextCommands
    {
        public MainView()
        {
            this.InitializeComponent();
            this.InitializePresenter<MainViewPresenter>();

            this.PlayerView.DataContext = this.Presenter<MainViewPresenter>().PlayerViewPresenter;
            this.SnappedPlayerView.DataContext = this.Presenter<MainViewPresenter>().PlayerViewPresenter;

            Debug.Assert(this.BottomAppBar != null, "this.BottomAppBar != null");
            this.BottomAppBar.Opened += (sender, o) =>
                {
                    if (this.BottomAppBar.Visibility == Visibility.Collapsed)
                    {
                        this.BottomAppBar.IsOpen = false;
                    }
                    else
                    {
                        this.BottomBorder.Visibility = Visibility.Visible;
                    }
                };

            this.BottomAppBar.Closed += (sender, o) => { this.BottomBorder.Visibility = Visibility.Collapsed; };

            this.Loaded += this.OnLoaded;

            CurrentApp.LicenseInformation.LicenseChanged += () => this.UpdateAdControl(false);
        }

        private bool IsAdFree()
        {
            return (CurrentApp.LicenseInformation.ProductLicenses.ContainsKey("AdFree")
                && CurrentApp.LicenseInformation.ProductLicenses["AddFree"].IsActive)
                || (CurrentApp.LicenseInformation.ProductLicenses.ContainsKey("Ultimate")
                && CurrentApp.LicenseInformation.ProductLicenses["Ultimate"].IsActive);
        }

        private void UpdateAdControl(bool forceHide)
        {
            this.AddControl.Visibility = this.IsAdFree() || forceHide ? Visibility.Collapsed : Visibility.Visible;
        }

        public void ShowView(IView view)
        {
            var visible = this.Presenter<MainViewPresenter>().BindingModel.IsAuthenticated
                          && this.Presenter<MainViewPresenter>().HasHistory()
                          && ApplicationView.Value != ApplicationViewState.Snapped;

            this.UpdateAppBars(visible);
            this.UpdateAdControl(!visible);

            this.ClearContext();
            this.Content.Content = view;
        }

        public void HideView()
        {
            this.ClearContext();
            this.Content.Content = null;
        }

        public MediaElement GetMediaElement()
        {
            return this.MediaElement;
        }

        public void Activate()
        {
            Debug.Assert(this.BottomAppBar != null, "this.BottomAppBar != null");
            this.BottomAppBar.IsOpen = true;
        }

        public void SetCommands(IEnumerable<UIElement> buttons)
        {
            this.ClearContext();
            if (buttons != null)
            {
                foreach (var buttonBase in buttons)
                {
                    this.ContextCommands.Children.Add(buttonBase);
                }

                this.Activate();
            }
        }

        public void ClearContext()
        {
            this.ContextCommands.Children.Clear(); 
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            this.Loaded -= this.OnLoaded;
            this.UpdateCurrentView();
            this.SizeChanged += (s, args) => this.UpdateCurrentView();
        }

        private void UpdateCurrentView()
        {
            if (ApplicationView.Value == ApplicationViewState.Snapped)
            {
                this.Content.Visibility = Visibility.Collapsed;
                this.BackButton.Visibility = Visibility.Collapsed;
                this.SnappedPlayerView.Visibility = Visibility.Visible;
            }
            else
            {
                this.Content.Visibility = Visibility.Visible;
                this.BackButton.Visibility = Visibility.Visible;
                this.SnappedPlayerView.Visibility = Visibility.Collapsed;
            }

            this.UpdateAppBars(ApplicationView.Value != ApplicationViewState.Snapped);
        }

        private void UpdateAppBars(bool visible)
        {
            if (visible)
            {
                this.BottomAppBar.IsEnabled = true;
                this.TopAppBar.IsEnabled = true;
                this.BottomAppBar.Visibility = Visibility.Visible;
                this.TopAppBar.Visibility = Visibility.Visible;
            }
            else
            {
                this.BottomAppBar.IsEnabled = false;
                this.TopAppBar.IsEnabled = false;
                this.BottomAppBar.IsOpen = false;
                this.TopAppBar.IsOpen = false;
                this.BottomAppBar.Visibility = Visibility.Collapsed;
                this.TopAppBar.Visibility = Visibility.Collapsed;
            }
        }

        private void GoBackClick(object sender, RoutedEventArgs e)
        {
            var mainViewPresenter = this.Presenter<MainViewPresenter>();
            if (mainViewPresenter.CanGoBack())
            {
                mainViewPresenter.GoBack();
            }
        }
        
        private void HomeNavigate(object sender, RoutedEventArgs e)
        {
            this.Navigate<IStartView>();
        }

        private void QueueNavigate(object sender, RoutedEventArgs e)
        {
            this.Navigate<ICurrentPlaylistView>();
        }

        private void PlaylistsNavigate(object sender, RoutedEventArgs e)
        {
            this.Navigate<IPlaylistsView>(PlaylistsRequest.Playlists);
        }

        private void AlbumsNavigate(object sender, RoutedEventArgs e)
        {
            this.Navigate<IPlaylistsView>(PlaylistsRequest.Albums);
        }

        private void GenresNavigate(object sender, RoutedEventArgs e)
        {
            this.Navigate<IPlaylistsView>(PlaylistsRequest.Genres);
        }

        private void ArtistsNavigate(object sender, RoutedEventArgs e)
        {
            this.Navigate<IPlaylistsView>(PlaylistsRequest.Artists);
        }

        private void Navigate<TView>(object parameter = null) where TView : IView
        {
            Debug.Assert(this.TopAppBar != null, "this.TopAppBar != null");
            this.TopAppBar.IsOpen = false;
            App.Container.Resolve<INavigationService>().NavigateTo<TView>(parameter: parameter);
        }
    }
}
