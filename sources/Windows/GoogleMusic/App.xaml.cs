﻿//--------------------------------------------------------------------------------------------------------------------
// Outcold Solutions (http://outcoldman.com)
//--------------------------------------------------------------------------------------------------------------------

namespace OutcoldSolutions.GoogleMusic
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading.Tasks;

    using BugSense;
    using BugSense.Core.Model;
    using BugSense.Model;

    using OutcoldSolutions.BindingModels;
    using OutcoldSolutions.Controls;
    using OutcoldSolutions.Diagnostics;
    using OutcoldSolutions.GoogleMusic.BindingModels;
    using OutcoldSolutions.GoogleMusic.Models;
    using OutcoldSolutions.GoogleMusic.Presenters.Popups;
    using OutcoldSolutions.GoogleMusic.Repositories;
    using OutcoldSolutions.GoogleMusic.Services;
    using OutcoldSolutions.GoogleMusic.Services.Publishers;
    using OutcoldSolutions.GoogleMusic.Shell;
    using OutcoldSolutions.GoogleMusic.Views;
    using OutcoldSolutions.GoogleMusic.Views.Popups;
    using OutcoldSolutions.GoogleMusic.Web;
    using OutcoldSolutions.GoogleMusic.Web.Lastfm;
    using OutcoldSolutions.GoogleMusic.Web.Synchronization;
    using OutcoldSolutions.Shell;
    using OutcoldSolutions.Views;

    using Windows.ApplicationModel;
    using Windows.UI.Notifications;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Media.Imaging;

    public sealed partial class App : ApplicationBase
    {
        public App()
        {
            this.InitializeComponent();
#if !DEBUG
            BugSense.BugSenseHandler.Instance.UnregisterUnobservedTaskExceptions();
            BugSense.BugSenseHandler.Instance.InitAndStartSession(new ExceptionManager(this), "w8c8d6b5");
            TaskScheduler.UnobservedTaskException += (sender, args) =>
                {
                    if (!args.Observed && args.Exception != null)
                    {
                        var exception = args.Exception.Flatten();
                        if (exception.InnerException != null 
                            && exception.InnerException.Message.IndexOf("fimpression", StringComparison.OrdinalIgnoreCase) < 0
                            && exception.InnerException.Message.IndexOf("No ad available", StringComparison.OrdinalIgnoreCase) < 0
                            && (exception.InnerException.InnerException == null
                                || !exception.InnerException.InnerException.Message.Contains("fimpression")))
                        {
                            var logExtra = new LimitedCrashExtraDataList()
                                               {
                                                   { "level", "UnobservedTaskException" }
                                               };
                            BugSense.BugSenseHandler.Instance.LogException(exception.InnerException, logExtra);
                        }

                        args.SetObserved();
                    }
                };
#endif
        }

        protected override void InitializeApplication()
        {
            this.Resources["ApplicationName"] = string.Format(CultureInfo.CurrentCulture, "gMusicW {0}", Package.Current.Id.Version.ToVersionString());

            using (var registration = Container.Registration())
            {
#if DEBUG
                registration.Register<IDebugConsole>().AsSingleton<DebugConsole>();
#endif

                Registration.RegisterPages(registration);
                Registration.RegisterSettingViews(registration);
                Registration.RegisterPopupViews(registration);
                Registration.RegisterViews(registration);

                registration.Register<SongsBindingModel>();

                registration.Register<IApplicationResources>().AsSingleton<ApplicationResources>();

                // Settings
                registration.Register<ISearchService>().AsSingleton<SearchService>();

                registration.Register<ILastfmAuthentificationView>()
                            .InjectionRule<BindingModelBase, LastfmAuthentificationPresenter>()
                            .As<LastfmAuthentificationPageView>();
                registration.Register<LastfmAuthentificationPresenter>();

                // Services
                registration.Register<IDataProtectService>().AsSingleton<DataProtectService>();
                registration.Register<IGoogleAccountWebService>().As<GoogleAccountWebService>();
                registration.Register<IGoogleMusicWebService>().AsSingleton<GoogleMusicWebService>();
                registration.Register<IGoogleMusicApisService>().AsSingleton<GoogleMusicApisService>();
                registration.Register<IGoogleAccountService>().AsSingleton<GoogleAccountService>();
                registration.Register<IAuthentificationService>().As<AuthentificationService>();
                registration.Register<IPlaylistsWebService>().As<PlaylistsWebService>();
                registration.Register<ISongsWebService>().AsSingleton<SongsWebService>();
                registration.Register<IRadioWebService>().AsSingleton<RadioWebService>();
                registration.Register<ISettingsService>().AsSingleton<SettingsService>();
                registration.Register<IGoogleMusicSessionService>().AsSingleton<GoogleMusicSessionService>();
                registration.Register<IConfigWebService>().AsSingleton<ConfigWebService>();

                registration.Register<IMediaStreamDownloadService>().AsSingleton<MediaStreamDownloadService>();

                registration.Register<ILastfmWebService>().AsSingleton<LastfmWebService>();
                registration.Register<ILastfmAccountWebService>().As<LastfmAccountWebService>();

                // Publishers
                registration.Register<ICurrentSongPublisherService>().AsSingleton<CurrentSongPublisherService>();
                registration.Register<GoogleMusicCurrentSongPublisher>().AsSingleton();
                registration.Register<MediaControlCurrentSongPublisher>().AsSingleton();
                registration.Register<TileCurrentSongPublisher>().AsSingleton();
                registration.Register<LastFmCurrentSongPublisher>().AsSingleton();

                // Songs Repositories and Services
                registration.Register<ICachedSongsRepository>().AsSingleton<CachedSongsRepository>();
                registration.Register<ISongsRepository>().AsSingleton<SongsRepository>();
                registration.Register<IUserPlaylistsRepository>().And<IPlaylistRepository<UserPlaylist>>().AsSingleton<UserPlaylistsRepository>();
                registration.Register<IArtistsRepository>().And<IPlaylistRepository<Artist>>().AsSingleton<ArtistsRepository>();
                registration.Register<IAlbumsRepository>().And<IPlaylistRepository<Album>>().AsSingleton<AlbumsRepository>();
                registration.Register<IGenresRepository>().And<IPlaylistRepository<Genre>>().AsSingleton<GenresRepository>();
                registration.Register<ISystemPlaylistsRepository>().And<IPlaylistRepository<SystemPlaylist>>().AsSingleton<SystemPlaylistsRepository>();
                registration.Register<IPlaylistsService>().AsSingleton<PlaylistsService>();
                registration.Register<IUserPlaylistsService>().AsSingleton<UserPlaylistsService>();
                registration.Register<IAlbumArtCacheService>().AsSingleton<AlbumArtCacheService>();
                registration.Register<ICachedAlbumArtsRepository>().AsSingleton<CachedAlbumArtsRepository>();
                registration.Register<IRadioStationsRepository>().And<IPlaylistRepository<Radio>>().AsSingleton<RadioStationsRepository>();
                registration.Register<IRadioStationsService>().AsSingleton<RadioStationsService>();

                registration.Register<IInitialSynchronization>().As<InitialSynchronization>();

                registration.Register<ISongsService>().AsSingleton<SongsService>();

                registration.Register<RightRegionControlService>().AsSingleton();
                registration.Register<ApplicationLogManager>().AsSingleton();

                registration.Register<ISongsCachingService>().AsSingleton<SongsCachingService>();

                registration.Register<IApplicationStateService>().AsSingleton<ApplicationStateService>();

                registration.Register<MediaElement>()
                            .AsSingleton(
                                new MediaElement()
                                    {
                                        IsLooping = false,
                                        AutoPlay = true,
                                        AudioCategory = AudioCategory.BackgroundCapableMedia
                                    });

                registration.Register<IMediaElementContainer>()
                            .AsSingleton<MediaElementContainer>();

                registration.Register<IPlayQueueService>()
                            .AsSingleton<PlayQueueService>();

                registration.Register<INotificationService>()
                            .AsSingleton<NotificationService>();

                registration.Register<MediaControlIntegration>();

                registration.Register<IGoogleMusicSynchronizationService>()
                            .AsSingleton<GoogleMusicSynchronizationService>();

                registration.Register<ScreenLocker>();
                registration.Register<ApplicationStateChangeHandler>();
            }

            Container.Resolve<ApplicationLogManager>();

            Container.Resolve<IGoogleMusicSessionService>().LoadSession();

            // Publishers
            var currentSongPublisherService = Container.Resolve<ICurrentSongPublisherService>();
            currentSongPublisherService.AddPublisher<GoogleMusicCurrentSongPublisher>();
            currentSongPublisherService.AddPublisher<MediaControlCurrentSongPublisher>();
            currentSongPublisherService.AddPublisher<TileCurrentSongPublisher>();

            if (Container.Resolve<ILastfmWebService>().RestoreSession())
            {
                currentSongPublisherService.AddPublisher<LastFmCurrentSongPublisher>();
            }

            Container.Resolve<ISearchService>();
        }

        protected override void OnActivated(bool isFirstTimeActivated)
        {
            if (isFirstTimeActivated)
            {
                Container.Resolve<RightRegionControlService>();

                Container.Resolve<ApplicationStateChangeHandler>();

                var mainFrameRegionProvider = Container.Resolve<IMainFrameRegionProvider>();
                mainFrameRegionProvider.SetContent(
                    MainFrameRegion.Background,
                    new Image()
                    {
                        Source = new BitmapImage(new Uri("ms-appx:///Resources/logo460.png")),
                        Height = 460,
                        Width = 460,
                        Margin = new Thickness(20),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top,
                        Opacity = 0.02
                    });

                mainFrameRegionProvider.SetContent(MainFrameRegion.BottomAppBarRightZone, Container.Resolve<IPlayerView>());
                mainFrameRegionProvider.SetContent(MainFrameRegion.SnappedView, Container.Resolve<ISnappedPlayerView>());
                mainFrameRegionProvider.SetContent(MainFrameRegion.TopAppBarRightZone, new LogoView());

                var page = (Page)Window.Current.Content;
                VisualTreeHelperEx.GetVisualChild<Panel>(page).Children.Add(Container.Resolve<MediaElement>());

                MainMenu.Initialize(
                    Container.Resolve<IMainFrame>(),
                    Container.Resolve<IApplicationResources>(),
                    Container.Resolve<IApplicationStateService>(),
                    Container.Resolve<ISettingsService>(),
                    Container.Resolve<IEventAggregator>());
                ApplicationSettingViews.Initialize(Container.Resolve<IApplicationSettingViewsService>(), Container.Resolve<IApplicationResources>());

                Container.Resolve<MediaControlIntegration>();
                Container.Resolve<ScreenLocker>();

                Container.Resolve<INavigationService>().NavigateTo<IStartPageView>();
            }
        }

        protected override async Task OnSuspendingAsync()
        {
            await Container.Resolve<IGoogleMusicSessionService>().SaveCurrentSessionAsync();

            Container.Resolve<ILastfmWebService>().SaveCurrentSession();

            TileUpdateManager.CreateTileUpdaterForApplication().Clear();
        }

#if DEBUG
        private class DebugConsole : IDebugConsole
        {
            public void WriteLine(string message)
            {
                Debug.WriteLine(message);
            }
        }
#endif
    }
}
