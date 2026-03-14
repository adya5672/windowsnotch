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
        double[] weights = {0.6,0.8,1.0,0.8,0.6 };
        double[] maxHeights= {14,18,24,18,14 };
        double[] heights;
        Random random = new Random();
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

            if(currentSession !=null)
            {
                var mediaProperties = await currentSession.TryGetMediaPropertiesAsync();
                SongTitleText.Text = mediaProperties.Title;
                ArtistText.Text = mediaProperties.Artist;
                await LoadAlbumArt(mediaProperties);
            }
            else
            {
                MessageBox.Show("No media sessions found.");
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
            heights = new double[bars.Length];
            for(int i=0;i<bars.Length;i++)
            {
                heights[i] = bars[i].Height;
            }
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(90);
            timer.Tick += UpdateWaveform;
            mediaManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            currentSession = mediaManager.GetCurrentSession();
            currentSession.MediaPropertiesChanged += CurrentSession_MediaPropertiesChanged;
            collapseTimer.Interval = TimeSpan.FromSeconds(4);
            collapseTimer.Tick += (s, e) =>
            {
                CollapseWidget();
                collapseTimer.Stop();
            };
            timer.Start();
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
            DoubleAnimation widthAnim= new DoubleAnimation
            {
                From = Width,
                To=280,
                Duration=TimeSpan.FromMilliseconds(250),
                EasingFunction=new QuadraticEase()
            };
            BeginAnimation(Window.WidthProperty, widthAnim);
        }
        private async Task LoadAlbumArt(GlobalSystemMediaTransportControlsSessionMediaProperties mediaProperties)
        {
            var thumbnail = mediaProperties.Thumbnail;
            if (thumbnail == null)
                return;
            var stream = await thumbnail.OpenReadAsync();
            using (var netStream = stream.AsStreamForRead())
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = netStream;
                bitmap.EndInit();
                bitmap.Freeze();

                Dispatcher.Invoke(() =>
                {
                    AlbumArtImage.Source = bitmap;
                });
            }
        }
        void UpdateWaveform(object sender,EventArgs e) {
            if(currentSession==null)
            {
                return;
            }
            int baseHeight = random.Next(8, 22);
            for(int i=0; i < bars.Length; i++)
            {
                double height = baseHeight * weights[i];
                height += random.Next(-3, 4);
                height = Math.Clamp(height, 8, maxHeights[i]);
                heights[i] = heights[i] + (height - heights[i]) * 0.25;
                bars[i].Height = heights[i];
            }
        }
    }
}
