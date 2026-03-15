using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Foundation;
using Windows.Storage.Streams;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;

namespace windowsnotch
{
    public partial class MainWindow : Window
    {
        Border[] bars;
        double[] heights;
        double[] targets;
        double[] lerpSpeeds;
        double[] envelopeWeights = { 0.45, 0.75, 1.0, 0.75, 0.45 };
        Random random = new Random();
        bool isHovered = false;

        DispatcherTimer timer = new DispatcherTimer();
        DispatcherTimer collapseTimer = new DispatcherTimer();
        GlobalSystemMediaTransportControlsSessionManager mediaManager;
        GlobalSystemMediaTransportControlsSession currentSession;

        public MainWindow()
        {
            InitializeComponent();
            GetMediaInfo();
            Loaded += MainWindow_Loaded;
        }

        private async void GetMediaInfo()
        {
            mediaManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            currentSession = mediaManager.GetCurrentSession();

            if (currentSession != null)
            {
                var mediaProperties = await currentSession.TryGetMediaPropertiesAsync();
                SongTitleText.Text = mediaProperties.Title;
                ArtistText.Text = mediaProperties.Artist;
                await LoadAlbumArt(mediaProperties);
            }
            else
            {
                SongTitleText.Text = "Nothing Playing";
                ArtistText.Text = "Open Spotify or Another media app";
                AlbumArtImage.Source = null;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Width = 280;
            Height = 80;
            Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
            Top = -10;

            bars = new Border[] { Bar1, Bar2, Bar3, Bar4, Bar5 };
            int n = bars.Length;

            heights = new double[n];
            targets = new double[n];
            lerpSpeeds = new double[n];

            for (int i = 0; i < n; i++)
            {
                heights[i] = 6;
                targets[i] = 6;
                lerpSpeeds[i] = 0.08 + random.NextDouble() * 0.14;
            }

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(40);
            timer.Tick += UpdateWaveform;

            mediaManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            mediaManager.CurrentSessionChanged += MediaManager_CurrentSessionChanged;
            currentSession = mediaManager.GetCurrentSession();

            if (currentSession != null)
                currentSession.MediaPropertiesChanged += CurrentSession_MediaPropertiesChanged;

            collapseTimer.Interval = TimeSpan.FromSeconds(4);
            collapseTimer.Tick += (s, ev) =>
            {
                CollapseWidget();
                collapseTimer.Stop();
            };

            timer.Start();
        }

        private async void MediaManager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            currentSession = sender.GetCurrentSession();
            if (currentSession == null)
            {
                Dispatcher.Invoke(() =>
                {
                    SongTitleText.Text = "Nothing Playing";
                    ArtistText.Text = "";
                    AlbumArtImage.Source = null;
                });
                return;
            }
            var mediaProperties = await currentSession.TryGetMediaPropertiesAsync();
            Dispatcher.Invoke(() =>
            {
                SongTitleText.Text = mediaProperties.Title;
                ArtistText.Text = mediaProperties.Artist;
            });
            await LoadAlbumArt(mediaProperties);
            currentSession.MediaPropertiesChanged += CurrentSession_MediaPropertiesChanged;
        }

        private void Widget_MouseEnter(object sender, MouseEventArgs e)
        {
            isHovered = true;
            ExpandWidget();
            FadeElement(WaveformBars, 0.05);
            FadeElement(PlaybackControls, 1);
        }

        private void Widget_MouseLeave(object sender, MouseEventArgs e)
        {
            isHovered = false;
            CollapseWidget();
            FadeElement(WaveformBars, 1);
            FadeElement(PlaybackControls, 0);
        }

        private void FadeElement(UIElement element, double targetOpacity)
        {
            DoubleAnimation fade = new DoubleAnimation
            {
                To = targetOpacity,
                Duration = TimeSpan.FromMilliseconds(200)
            };
            element.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        private async void CurrentSession_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            var mediaProperties = await sender.TryGetMediaPropertiesAsync();
            Dispatcher.Invoke(() =>
            {
                SongTitleText.Text = mediaProperties.Title;
                ArtistText.Text = mediaProperties.Artist;
                ExpandWidget();
                collapseTimer.Stop();
                collapseTimer.Start();
            });
            await LoadAlbumArt(mediaProperties);
        }

        private void ExpandWidget()
        {
            DoubleAnimation widthAnim = new DoubleAnimation
            {
                From = ActualWidth,
                To = 360,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuadraticEase()
            };
            BeginAnimation(Window.WidthProperty, widthAnim);
        }

        private void CollapseWidget()
        {
            DoubleAnimation widthAnim = new DoubleAnimation
            {
                From = Width,
                To = 280,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuadraticEase()
            };
            BeginAnimation(Window.WidthProperty, widthAnim);
        }

        private async Task LoadAlbumArt(GlobalSystemMediaTransportControlsSessionMediaProperties mediaProperties)
        {
            var thumbnail = mediaProperties.Thumbnail;
            if (thumbnail == null) return;

            var stream = await thumbnail.OpenReadAsync();
            using (var netStream = stream.AsStreamForRead())
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = netStream;
                bitmap.EndInit();
                bitmap.Freeze();
                Dispatcher.Invoke(() => AlbumArtImage.Source = bitmap);
            }
        }

        void UpdateWaveform(object sender, EventArgs e)
        {
            if (currentSession == null)
            {
                SongTitleText.Text = "Nothing Playing";
                return;
            }

            var playback = currentSession.GetPlaybackInfo();
            if (playback.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                for (int i = 0; i < bars.Length; i++)
                {
                    heights[i] = heights[i] + (6 - heights[i]) * 0.12;
                    bars[i].Height = heights[i];
                }
                if (!isHovered && WaveformBars.Opacity != 0.4)
                    FadeElement(WaveformBars, 0.4);
                return;
            }

            if (!isHovered && WaveformBars.Opacity != 1.0)
                FadeElement(WaveformBars, 1.0);

            for (int i = 0; i < bars.Length; i++)
            {
                if (Math.Abs(heights[i] - targets[i]) < 1.2)
                {
                    double maxH = 38 * envelopeWeights[i];
                    targets[i] = 6 + random.NextDouble() * (maxH - 6);
                }

                heights[i] = heights[i] + (targets[i] - heights[i]) * lerpSpeeds[i];
                bars[i].Height = heights[i];
            }
        }

        private async void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (currentSession == null) return;
            var playbackInfo = currentSession.GetPlaybackInfo();
            if (playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                await currentSession.TryPauseAsync();
            else
                await currentSession.TryPlayAsync();
        }

        private async void Next_Click(object sender, RoutedEventArgs e)
        {
            if (currentSession == null) return;
            await currentSession.TrySkipNextAsync();
        }

        private async void Prev_Click(object sender, RoutedEventArgs e)
        {
            if (currentSession == null) return;
            await currentSession.TrySkipPreviousAsync();
        }
    }
}