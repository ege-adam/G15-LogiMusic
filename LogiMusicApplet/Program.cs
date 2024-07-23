using LogiFrame;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using Windows.Media.Control;
using Windows.UI.Xaml.Media.Imaging;

namespace LogiMusicApplet
{
    class Program
    {
        private static GlobalSystemMediaTransportControlsSessionManager sessionManager;
        private static GlobalSystemMediaTransportControlsSession currentSession;
        private static GlobalSystemMediaTransportControlsSessionMediaProperties mediaProps;
        private static GlobalSystemMediaTransportControlsSessionPlaybackStatus playbackStatus;
        private static GlobalSystemMediaTransportControlsSessionTimelineProperties timeLineProps;

        private static LCDPicture lcdMediaArt;
        private static LCDLabel lcdCurrentlyPlaying;
        private static LCDMarquee lcdMediaTitle;
        private static LCDLabel lcdStatus;
        private static LCDLabel lcdArtist;
        private static LCDApp lcdApp;
        private static LCDProgressBar lcdBar;
        private static TrayHelper trayHelper;

        static async Task Main()
        {
            InitializeControls();

            trayHelper = new TrayHelper();
            sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            sessionManager.CurrentSessionChanged += OnSessionChanged;

            await UpdateSessionInfo();

            lcdApp.PushToForeground();
            lcdApp.WaitForClose();
        }

        private static void InitializeControls()
        {
            lcdMediaTitle = new LCDMarquee
            {
                Font = PixelFonts.Small,
                Size = new Size(LCDApp.DefaultSize.Width, PixelFonts.Small.Height),
                Location = new Point(48, 12),
            };

            lcdMediaTitle.Font = new Font(PixelFonts.Small, FontStyle.Bold);

            lcdArtist = new LCDLabel
            {
                Font = PixelFonts.Small,
                Size = new Size(LCDApp.DefaultSize.Width - 48, PixelFonts.Small.Height),
                Location = new Point(48, 12 + PixelFonts.Small.Height),
                TextAlign = ContentAlignment.MiddleCenter,
            };

            lcdStatus = new LCDLabel
            {
                Font = PixelFonts.Small,
                Size = new Size(LCDApp.DefaultSize.Width - 48, PixelFonts.Small.Height),
                Location = new Point(48, 29),
                Text = "⏹",
                TextAlign = ContentAlignment.MiddleCenter,
            };

            lcdCurrentlyPlaying = new LCDLabel
            {
                Font = PixelFonts.Title,
                Text = "Currently Playing",
                Size = new Size(LCDApp.DefaultSize.Width - 48, PixelFonts.Title.Height),
                Location = new Point(48, 2),
                TextAlign = ContentAlignment.MiddleCenter,
            };

            lcdBar = new LCDProgressBar
            {
                Location = new Point(12 + 48, 37),
                Size = new Size(136 - 48, 6),
                Style = LogiFrame.BorderStyle.Border,
                Direction = ProgressBarDirection.Right,
                Value = 50
            };

            lcdMediaArt = new LCDPicture
            {
                Location = new Point(0, 0),
                Size = new Size(48, 48),
            };

            lcdApp = new LCDApp("LogiMusic", false, false, false);

            lcdApp.Controls.Add(lcdMediaTitle);
            lcdApp.Controls.Add(lcdArtist);
            lcdApp.Controls.Add(lcdCurrentlyPlaying);
            lcdApp.Controls.Add(lcdBar);
            lcdApp.Controls.Add(lcdStatus);
            lcdApp.Controls.Add(lcdMediaArt);
        }

        private static async void OnSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            await UpdateSessionInfo();
        }

        private static async Task UpdateSessionInfo()
        {
            currentSession = sessionManager.GetCurrentSession();

            if (currentSession != null)
            {
                WireSessionEvents();
                await UpdateMediaInfoAsync();
                UpdatePlaybackInfo();
            }
        }

        private static void WireSessionEvents()
        {
            currentSession.TimelinePropertiesChanged -= OnTimeLineUpdated;
            currentSession.MediaPropertiesChanged -= async (s, e) => { await OnMediaChangedAsync(s, e); };
            currentSession.PlaybackInfoChanged -= OnPlaybackUpdated;

            currentSession.TimelinePropertiesChanged += OnTimeLineUpdated;
            currentSession.MediaPropertiesChanged += async (s, e) => { await OnMediaChangedAsync(s, e); };
            currentSession.PlaybackInfoChanged += OnPlaybackUpdated;
        }

        private static async Task UpdateMediaInfoAsync()
        {
            mediaProps = await currentSession.TryGetMediaPropertiesAsync();

            if (mediaProps != null)
            {
                lcdMediaTitle.Text = mediaProps.Title;
                lcdArtist.Text = mediaProps.Artist;
                if (mediaProps.Thumbnail != null)
                {
                    lcdMediaArt.Image = ResizeImage(Image.FromStream((await mediaProps.Thumbnail.OpenReadAsync()).AsStreamForRead()), new Size(48, 48));
                }
            }
        }

        private static void UpdatePlaybackInfo()
        {
            UpdateTimeline();
        }

        public static void OnTimeLineUpdated(GlobalSystemMediaTransportControlsSession session, TimelinePropertiesChangedEventArgs arg)
        {
            currentSession = session;
            UpdateTimeline();
        }

        private static void UpdateTimeline()
        {
            if (currentSession == null) return;

            GlobalSystemMediaTransportControlsSessionPlaybackInfo playbackInfo = currentSession.GetPlaybackInfo();

            if (playbackInfo != null)
            {
                playbackStatus = playbackInfo.PlaybackStatus;
            }
            else
            {
                playbackStatus = GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped;
            }

            switch (playbackStatus)
            {
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing:
                    lcdStatus.Text = "▶ ";
                    break;
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused:
                    lcdStatus.Text = "⏸ ";
                    break;
                default:
                    lcdStatus.Text = "⏹ ";
                    break;
            }

            timeLineProps = currentSession.GetTimelineProperties();
            
            if (timeLineProps != null)
            {
                var pos = timeLineProps.Position;
                lcdStatus.Text += pos.ToString(@"hh\:mm\:ss");

                var percentage = (pos.TotalSeconds / timeLineProps.EndTime.TotalSeconds) * 100d;
                lcdBar.Value = (int)percentage;
            }
        }

        private static void OnPlaybackUpdated(GlobalSystemMediaTransportControlsSession session, PlaybackInfoChangedEventArgs args)
        {
            currentSession = session;
            UpdatePlaybackInfo();
        }

        public static async Task OnMediaChangedAsync(GlobalSystemMediaTransportControlsSession session, MediaPropertiesChangedEventArgs _args)
        {
            currentSession = session;
            await UpdateMediaInfoAsync();
        }

        private static System.Drawing.Image ResizeImage(System.Drawing.Image imgToResize, Size size)
        {
            int sourceWidth = imgToResize.Width;
            int sourceHeight = imgToResize.Height;

            float nPercent = Math.Min((float)size.Width / sourceWidth, (float)size.Height / sourceHeight);

            int destWidth = (int)(sourceWidth * nPercent);
            int destHeight = (int)(sourceHeight * nPercent);

            Bitmap b = new Bitmap(destWidth, destHeight);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.InterpolationMode = InterpolationMode.Low;
                g.DrawImage(imgToResize, 0, 0, destWidth, destHeight);
            }

            return b;
        }
    }
}
