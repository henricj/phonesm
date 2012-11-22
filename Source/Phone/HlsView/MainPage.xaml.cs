using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Phone.Controls;
using SM.Media;
using SM.Media.Playlists;

namespace HlsView
{
    public partial class MainPage : PhoneApplicationPage
    {
        readonly DispatcherTimer _positionSampler;
        int _positionSampleCount;
        TimeSpan _previousPosition;
        TsMediaManager _tsMediaManager;

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            _positionSampler = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(75) };
            _positionSampler.Tick += OnPositionSamplerOnTick;

            mediaElement1.CurrentStateChanged += (sender, args) => MediaStateBox.Text = mediaElement1.CurrentState.ToString();
        }

        void OnPositionSamplerOnTick(object o, EventArgs ea)
        {
            var positionSample = mediaElement1.Position;

            if (positionSample == _previousPosition)
                return;

            _previousPosition = positionSample;

            _tsMediaManager.ReportPosition(positionSample);

            if (++_positionSampleCount > 2)
            {
                _positionSampleCount = 0;

                var positionText = positionSample.ToString();

                PositionBox.Text = positionText; //.Substring(0, positionText.Length - 5);
            }
        }

        void play_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Play clicked");

            errorBox.Visibility = Visibility.Collapsed;
            playButton.IsEnabled = false;

            CleanupMedia();

            var simpleSegmentManager = new PlaylistSegmentManager(new Uri("http://www.nasa.gov/multimedia/nasatv/NTV-Public-IPS.m3u8"));

            _tsMediaManager = new TsMediaManager(mediaElement1);

            _tsMediaManager.Play(simpleSegmentManager);

            _positionSampler.Start();
        }

        void mediaElement1_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            errorBox.Text = e.ErrorException.Message;
            errorBox.Visibility = Visibility.Visible;

            CleanupMedia();

            playButton.IsEnabled = true;
        }

        void CleanupMedia()
        {
            _positionSampler.Stop();

            if (null != _tsMediaManager)
                _tsMediaManager.Stop();
        }

        void mediaElement1_MediaEnded(object sender, RoutedEventArgs e)
        {
            CleanupMedia();

            playButton.IsEnabled = true;
        }

        void stopButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Stop clicked");

            CleanupMedia();

            playButton.IsEnabled = true;

            //mediaElement1.Stop();
        }

        void wakeButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Wake clicked");

            var state = mediaElement1.CurrentState;
        }
    }
}