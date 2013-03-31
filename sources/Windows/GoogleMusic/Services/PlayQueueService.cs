﻿// --------------------------------------------------------------------------------------------------------------------
// Outcold Solutions (http://outcoldman.com)
// --------------------------------------------------------------------------------------------------------------------
namespace OutcoldSolutions.GoogleMusic.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using OutcoldSolutions.Diagnostics;
    using OutcoldSolutions.GoogleMusic.Models;
    using OutcoldSolutions.GoogleMusic.Services.Publishers;
    using OutcoldSolutions.GoogleMusic.Shell;
    using OutcoldSolutions.GoogleMusic.Web;
    using OutcoldSolutions.GoogleMusic.Web.Models;

    public class PlayQueueService : IPlayQueueService
    {
        private readonly ILogger logger;
        private readonly IMediaElementContainer mediaElement;
        private readonly ISettingsService settingsService;
        private readonly ICurrentSongPublisherService publisherService;
        private readonly ISongsWebService songsWebService;
        private readonly INotificationService notificationService;
        private readonly IPlaylistsService playlistsService;
        private readonly IEventAggregator eventAggregator;

        private readonly List<Song> songsQueue = new List<Song>();
        private readonly List<int> queueOrder = new List<int>();

        private readonly Random random = new Random((int)DateTime.Now.Ticks);

        private readonly IMediaStreamDownloadService downloadService;

        private INetworkRandomAccessStream currentSongStream;
        private int currentQueueIndex; // From queueOrder

        private IPlaylist currentPlaylist;

        private QueueState state;

        private bool isShuffled;

        private bool isRepeatAll;

        public PlayQueueService(
            ILogManager logManager,
            IMediaElementContainer mediaElement,
            ISettingsService settingsService,
            IMediaStreamDownloadService downloadService,
            ICurrentSongPublisherService publisherService,
            ISongsWebService songsWebService,
            INotificationService notificationService,
            IGoogleMusicSessionService sessionService,
            IPlaylistsService playlistsService,
            IEventAggregator eventAggregator)
        {
            this.logger = logManager.CreateLogger("PlayQueueService");
            this.mediaElement = mediaElement;
            this.settingsService = settingsService;
            this.downloadService = downloadService;
            this.publisherService = publisherService;
            this.songsWebService = songsWebService;
            this.notificationService = notificationService;
            this.playlistsService = playlistsService;
            this.eventAggregator = eventAggregator;
            this.currentQueueIndex = -1;

            this.IsRepeatAll = this.settingsService.GetValue("IsRepeatAllEnabled", defaultValue: false);
            this.IsShuffled = this.settingsService.GetValue("IsShuffleEnabled", defaultValue: false);

            this.State = QueueState.Unknown;

            this.mediaElement.MediaEnded += async (sender, args) =>
                {
                    if (this.CanSwitchToNext())
                    {
                        await this.NextSongAsync();
                    }
                };

            sessionService.SessionCleared += async (sender, args) =>
                {
                    await this.StopAsync();
                    this.queueOrder.Clear();
                    this.songsQueue.Clear();
                    this.currentQueueIndex = -1;
                    this.currentSongStream = null;
                    this.currentPlaylist = null;
                };
        }

        public event EventHandler QueueChanged;

        public event EventHandler<StateChangedEventArgs> StateChanged;

        public event EventHandler<double> DownloadProgress;

        public bool IsShuffled
        {
            get
            {
                return this.isShuffled;
            }
            
            set
            {
                if (this.isShuffled != value)
                {
                    this.isShuffled = value;
                    this.settingsService.SetValue("IsShuffleEnabled", this.isShuffled);

                    lock (this.queueOrder)
                    {
                        this.UpdateOrder();
                    }

                    this.RaiseQueueChanged();
                }
            }
        }

        public bool IsRepeatAll
        {
            get
            {
                return this.isRepeatAll;
            }
            
            set
            {
                if (this.isRepeatAll != value)
                {
                    this.isRepeatAll = value;
                    this.settingsService.SetValue("IsRepeatAllEnabled", this.isRepeatAll);
                    this.RaiseQueueChanged();
                }
            }
        }

        public QueueState State
        {
            get
            {
                return this.state;
            }

            private set
            {
                this.state = value;

                if (value == QueueState.Play || value == QueueState.Paused)
                {
                    this.RaiseStateChanged(new StateChangedEventArgs(value, this.songsQueue[this.CurrentSongIndex]));
                }
                else
                {
                    this.RaiseStateChanged(new StateChangedEventArgs(value));
                }
            }
        }

        private int CurrentSongIndex
        {
            get
            {
                if (this.currentQueueIndex >= this.queueOrder.Count || this.currentQueueIndex < 0)
                {
                    return -1;
                }

                return this.queueOrder[this.currentQueueIndex];
            }
        }

        public async Task PlayAsync(IPlaylist playlist)
        {
            await this.PlayAsync(playlist, songIndex: -1);
        }

        public async Task PlayAsync(IPlaylist playlist, int songIndex)
        {
            if (playlist == null)
            {
                throw new ArgumentNullException("playlist");
            }

            var songs = await this.playlistsService.GetSongsAsync(playlist);
            await this.PlayAsync(playlist, songs, songIndex);
        }
        
        public async Task PlayAsync(IPlaylist playlist, IEnumerable<Song> songs, int songIndex)
        {
            if (playlist == null)
            {
                throw new ArgumentNullException("playlist");
            }

            await Task.Run(async () =>
            {
                if (this.State == QueueState.Busy)
                {
                    throw new InvalidOperationException("Queue is busy");
                }

                this.currentPlaylist = playlist;
                this.songsQueue.Clear();
                this.songsQueue.AddRange(songs);

                this.RaiseQueueChanged();

                this.UpdateOrder(songIndex);

                if (this.currentQueueIndex >= 0)
                {
                    await this.PlaySongAsyncInternal(this.CurrentSongIndex);
                }
                else
                {
                    await this.StopAsync();
                }
            });
        }

        public async Task PlayAsync(int songIndex)
        {
            await Task.Run(async () =>
            {
                if (this.State == QueueState.Busy)
                {
                    throw new InvalidOperationException("Queue is busy");
                }

                this.currentQueueIndex = this.queueOrder.IndexOf(songIndex);

                await this.PlaySongAsyncInternal(this.CurrentSongIndex);
            });
        }

        public async Task PlayAsync()
        {
            if (this.State == QueueState.Busy)
            {
                throw new InvalidOperationException("Queue is busy");
            }

            if (this.State == QueueState.Paused)
            {
                await this.mediaElement.PlayAsync();
                this.State = QueueState.Play;
                this.logger.LogTask(this.publisherService.PublishAsync(this.songsQueue[this.CurrentSongIndex], this.currentPlaylist));
            }
            else
            {
                await this.PlaySongAsyncInternal(this.CurrentSongIndex);
            }
        }

        public async Task StopAsync()
        {
            if (this.State == QueueState.Play || this.State == QueueState.Paused)
            {
                this.publisherService.CancelActiveTasks();
                await this.mediaElement.StopAsync();
                this.State = QueueState.Stopped;
            }
        }

        public async Task NextSongAsync()
        {
            if (this.State == QueueState.Busy)
            {
                throw new InvalidOperationException("Queue is busy");
            }

            if (this.CanSwitchToNext())
            {
                if (this.currentQueueIndex == (this.queueOrder.Count - 1) && this.IsRepeatAll)
                {
                    this.currentQueueIndex = 0;
                }
                else
                {
                    this.currentQueueIndex++;
                }

                await this.PlaySongAsyncInternal(this.currentQueueIndex);
            }
        }

        public bool CanSwitchToNext()
        {
            return this.currentQueueIndex < (this.queueOrder.Count - 1) || (this.IsRepeatAll && this.queueOrder.Count > 0);
        }

        public async Task PreviousSongAsync()
        {
            if (this.State == QueueState.Busy)
            {
                throw new InvalidOperationException("Queue is busy");
            }

            if (this.CanSwitchToPrevious())
            {
                if (this.currentQueueIndex != 0)
                {
                    this.currentQueueIndex--;
                }
                else if (this.IsRepeatAll)
                {
                    this.currentQueueIndex = this.queueOrder.Count - 1;
                }

                await this.PlaySongAsyncInternal(this.currentQueueIndex);
            }
        }

        public bool CanSwitchToPrevious()
        {
            return this.currentQueueIndex > 0 || (this.IsRepeatAll && this.queueOrder.Count > 0);
        }

        public async Task PauseAsync()
        {
            if (this.State == QueueState.Play)
            {
                this.publisherService.CancelActiveTasks();
                await this.mediaElement.PauseAsync();
                this.State = QueueState.Paused;
            }
        }

        public async Task AddRangeAsync(IEnumerable<Song> songs)
        {
            if (songs == null)
            {
                throw new ArgumentNullException("songs");
            }

            await Task.Run(() =>
            {
                this.currentPlaylist = null;

                var addedSongs = songs.ToList();
                
                var range = Enumerable.Range(this.songsQueue.Count, this.songsQueue.Count + addedSongs.Count);
                this.songsQueue.AddRange(addedSongs);

                if (this.IsShuffled)
                {
                    var shuffledQueue =
                        range.Select(
                            x => new
                            {
                                OrderIndex = this.random.Next(),
                                SongIndex = x
                            })
                            .OrderBy(x => x.OrderIndex)
                            .Select(x => x.SongIndex);

                    this.queueOrder.AddRange(shuffledQueue);
                }
                else
                {
                    this.queueOrder.AddRange(range);
                }

                this.RaiseQueueChanged();
            });
        }

        public async Task RemoveAsync(IEnumerable<int> songIndexes)
        {
            await Task.Run(async () =>
            {
                if (this.State == QueueState.Busy)
                {
                    throw new InvalidOperationException("Queue is busy");
                }

                List<int> collection = songIndexes.ToList();

                if (collection.Count > 0)
                {
                    this.currentPlaylist = null;

                    bool currentSongChanged = false;

                    foreach (int index in collection.OrderByDescending(x => x))
                    {
                        if (this.songsQueue.Count > index)
                        {
                            var queueIndex = this.queueOrder.IndexOf(index);

                            this.queueOrder.RemoveAt(queueIndex);

                            for (int i = 0; i < this.queueOrder.Count; i++)
                            {
                                if (this.queueOrder[i] > index)
                                {
                                    this.queueOrder[i]--;
                                }
                            }

                            this.songsQueue.RemoveAt(index);

                            if (this.songsQueue.Count == 0)
                            {
                                this.currentQueueIndex = -1;
                            }
                            else if (queueIndex == this.currentQueueIndex)
                            {
                                this.currentQueueIndex--;
                                currentSongChanged = true;
                            }
                            else if (queueIndex < this.currentQueueIndex)
                            {
                                this.currentQueueIndex--;
                            }
                        }
                    }

                    if (this.currentQueueIndex == -1)
                    {
                        await this.StopAsync();
                    }
                    else if (currentSongChanged)
                    {
                        await this.PlaySongAsyncInternal(this.CurrentSongIndex);
                    }

                    this.RaiseQueueChanged();
                }
            });
        }

        public IEnumerable<Song> GetQueue()
        {
            lock (this.queueOrder)
            {
                return this.songsQueue.ToList();
            }
        }

        public int GetCurrentSongIndex()
        {
            return this.CurrentSongIndex;
        }

        public Song GetCurrentSong()
        {
            return this.songsQueue[this.CurrentSongIndex];
        }

        private async Task PlaySongAsyncInternal(int songIndex)
        {
            var queueIndex = this.queueOrder.IndexOf(songIndex);

            if (this.logger.IsDebugEnabled)
            {
                this.logger.Debug("Play current song index: {0}.", queueIndex);
            }

            this.State = QueueState.Busy;

            this.publisherService.CancelActiveTasks();

            if (this.queueOrder.Count > queueIndex)
            {
                var song = this.songsQueue[songIndex];
                if (song != null)
                {
                    if (this.logger.IsDebugEnabled)
                    {
                        this.logger.Debug("Getting url for song '{0}'.", song.ProviderSongId);
                    }

                    GoogleMusicSongUrl songUrl = null;

                    try
                    {
                        songUrl = await this.songsWebService.GetSongUrlAsync(song.ProviderSongId);
                    }
                    catch (Exception e)
                    {
                        this.logger.LogErrorException(e);
                    }

                    if (songUrl != null)
                    {
                        if (this.logger.IsDebugEnabled)
                        {
                            this.logger.Debug("Getting stream by url '{0}'.", songUrl.Url);
                        }

                        if (this.currentSongStream != null)
                        {
                            this.logger.Debug("Current song is not null. Disposing current stream.");

                            await this.mediaElement.StopAsync();

                            this.currentSongStream.DownloadProgressChanged -= this.CurrentSongStreamOnDownloadProgressChanged;
                            this.currentSongStream.Dispose();
                            this.currentSongStream = null;
                        }

                        INetworkRandomAccessStream stream = null;

                        try
                        {
                            stream = await this.downloadService.GetStreamAsync(songUrl.Url);
                        }
                        catch (Exception exception)
                        {
                            if (exception is TaskCanceledException)
                            {
                                this.logger.Debug("GetStreamAsync was cancelled.");
                            }
                            else
                            {
                                this.logger.LogErrorException(exception);
                            }
                        }

                        this.currentSongStream = stream;

                        if (this.currentSongStream != null)
                        {
                            this.currentSongStream.DownloadProgressChanged += this.CurrentSongStreamOnDownloadProgressChanged;

                            await this.mediaElement.PlayAsync(this.currentSongStream, this.currentSongStream.ContentType);

                            this.State = QueueState.Play;

                            this.logger.LogTask(this.publisherService.PublishAsync(song, this.currentPlaylist));
                        }
                        else
                        {
                            this.State = QueueState.Stopped;

                            if (this.logger.IsWarningEnabled)
                            {
                                this.logger.Warning("Stream is null.");
                            }
                        }
                    }
                    else
                    {
                        this.State = QueueState.Stopped;

                        if (this.logger.IsDebugEnabled)
                        {
                            this.logger.Debug("Could not get url for song {0}.", song.ProviderSongId);
                        }

                        this.logger.LogTask(this.notificationService.ShowMessageAsync("Cannot play right now. Make sure that you don't use current account on different device at the same time. Try after couple minutes."));
                    }
                }
            }
            else
            {
                this.State = QueueState.Stopped;
            }
        }

        private void UpdateOrder()
        {
            this.UpdateOrder(this.CurrentSongIndex);
        }

        private void UpdateOrder(int firstSongIndex)
        {
            if (this.logger.IsDebugEnabled)
            {
                this.logger.Debug("Update order.");
            }

            this.queueOrder.Clear();

            if (this.songsQueue.Count > 0)
            {
                var range = Enumerable.Range(0, this.songsQueue.Count);

                if (this.IsShuffled)
                {
                    var shuffledQueue =
                        range.Select(
                            x => new
                                        {
                                            OrderIndex = (x == firstSongIndex) ? -1 : this.random.Next(),
                                            SongIndex = x
                                        })
                                .OrderBy(x => x.OrderIndex)
                                .Select(x => x.SongIndex);

                    this.queueOrder.AddRange(shuffledQueue);
                }
                else
                {
                    this.queueOrder.AddRange(range);
                }

                if (this.logger.IsInfoEnabled)
                {
                    this.logger.Info("Shuffle enabled: {0}", this.IsShuffled);
                    this.logger.Info("Playing order: {0}", string.Join(",", this.queueOrder));
                }

                this.currentQueueIndex = firstSongIndex >= 0 ? this.queueOrder.IndexOf(firstSongIndex) : 0;
            }
            else
            {
                this.currentQueueIndex = -1;
            }
        }

        private void CurrentSongStreamOnDownloadProgressChanged(object sender, double e)
        {
            this.RaiseDownloadProgress(e);
        }

        private void RaiseStateChanged(StateChangedEventArgs e)
        {
            var handler = this.StateChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private void RaiseDownloadProgress(double e)
        {
            var handler = this.DownloadProgress;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private void RaiseQueueChanged()
        {
            this.eventAggregator.Publish(new QueueChangeEvent(this.IsShuffled, this.IsRepeatAll, this.songsQueue));

            var handler = this.QueueChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}