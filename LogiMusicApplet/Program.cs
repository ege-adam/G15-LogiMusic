using LogiFrame;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Media.Control;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace LogiMusicApplet
{
    class Program
    {
        private static readonly int bgAfter = 10000;


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

        private static Task sleepTask;
        private static DateTime bgTime;

        private static TrayHelper trayHelper;

        static async Task Main()
        {
            trayHelper = new TrayHelper();

            // Create a control.
            lcdMediaTitle = new LCDMarquee
            {
                Font = PixelFonts.Small, // The PixelFonts class contains various good fonts for LCD screens.
                Size = new Size(LCDApp.DefaultSize.Width, PixelFonts.Small.Height),
                Location = new Point(48, 12),
            };

            //lcdMediaTitle.TextAlign = ContentAlignment.MiddleCenter;
            lcdMediaTitle.Font = new Font(PixelFonts.Small, FontStyle.Bold);

            lcdArtist = new LCDLabel
            {
                Font = PixelFonts.Small,
                Size = new Size(LCDApp.DefaultSize.Width - 48, PixelFonts.Small.Height),
                Location = new Point(48, 12 + PixelFonts.Small.Height),
            };

            lcdArtist.TextAlign = ContentAlignment.MiddleCenter;

            lcdStatus = new LCDLabel
            {
                Font = PixelFonts.Small,
                Size = new Size(LCDApp.DefaultSize.Width - 48, PixelFonts.Small.Height),
                Location = new Point(48, 29),
                Text = "⏹",
            };

            lcdStatus.TextAlign = ContentAlignment.MiddleCenter;

            lcdCurrentlyPlaying = new LCDLabel
            {
                Font = PixelFonts.Title,
                Text = "Currently Playing",
                Size = new Size(LCDApp.DefaultSize.Width - 48, PixelFonts.Title.Height),
                Location = new Point(48, 2),
            };

            lcdCurrentlyPlaying.TextAlign = ContentAlignment.MiddleCenter;

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

            // Create an app instance.
            lcdApp = new LCDApp("LogiMusic", false, false, false);

            lcdApp.Controls.Add(lcdMediaTitle);
            lcdApp.Controls.Add(lcdArtist);
            lcdApp.Controls.Add(lcdCurrentlyPlaying);
            lcdApp.Controls.Add(lcdBar);
            lcdApp.Controls.Add(lcdStatus);
            lcdApp.Controls.Add(lcdMediaArt);

            sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            sessionManager.CurrentSessionChanged += OnSessionChanged;

            await UpdateSessionInfo();


            // Make the app the foreground app on the LCD screen.
            lcdApp.PushToForeground();

            SendToBackground();
            lcdApp.WaitForClose();
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
                currentSession.TimelinePropertiesChanged -= OnTimeLineUpdated;
                currentSession.MediaPropertiesChanged -= async (s, e) => { await OnMediaChangedAsync(s, e); };
                currentSession.PlaybackInfoChanged -= OnPlaybackUpdated;

                currentSession.TimelinePropertiesChanged += OnTimeLineUpdated;
                currentSession.MediaPropertiesChanged += async (s, e) => { await OnMediaChangedAsync(s, e); };
                currentSession.PlaybackInfoChanged += OnPlaybackUpdated;

                await UpdateMediaInfoAsync();
                UpdatePlaybackInfo();
            }
        }


        private static async Task UpdateMediaInfoAsync()
        {
            if (currentSession == null) currentSession = sessionManager.GetCurrentSession();

            mediaProps = await currentSession.TryGetMediaPropertiesAsync();

            lcdMediaTitle.Text = mediaProps.Title;
            lcdArtist.Text = mediaProps.Artist;
            if (mediaProps.Thumbnail == null) return;
            lcdMediaArt.Image = ResizeImage(Image.FromStream((await mediaProps.Thumbnail.OpenReadAsync()).AsStreamForRead()), new Size(48, 48));
        }

        private static void UpdatePlaybackInfo()
        {
            if (!lcdApp.Visible) return;

            if (currentSession == null) currentSession = sessionManager.GetCurrentSession();

            if (currentSession == null) return;

            GlobalSystemMediaTransportControlsSessionPlaybackInfo playbackInfo = currentSession.GetPlaybackInfo();

            if (playbackInfo != null) playbackStatus = playbackInfo.PlaybackStatus;
            else playbackStatus = GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped;

            if (currentSession == null) return;
            timeLineProps = currentSession.GetTimelineProperties();


            switch (playbackStatus)
            {
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing:
                    lcdStatus.Text = "▶";
                    break;
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused:
                    lcdStatus.Text = "⏸";
                    break;
                default:
                    lcdStatus.Text = "⏹";
                    break;
            }

            lcdStatus.Text += timeLineProps.Position.ToString();

            lcdBar.Value = (int)(timeLineProps.Position.TotalSeconds / (timeLineProps.EndTime.TotalSeconds - timeLineProps.StartTime.TotalSeconds)) * 100;
        }

        public static void OnTimeLineUpdated(GlobalSystemMediaTransportControlsSession session, TimelinePropertiesChangedEventArgs arg)
        {
            UpdatePlaybackInfo();
        }

        private static void OnPlaybackUpdated(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            lcdApp.PushToForeground();

            UpdatePlaybackInfo();

            SendToBackground();
        }

        public static async Task OnMediaChangedAsync(GlobalSystemMediaTransportControlsSession session, MediaPropertiesChangedEventArgs _args)
        {
            lcdApp.PushToForeground();

            await UpdateSessionInfo();

            SendToBackground();
        }

        private static void SendToBackground()
        {
            bgTime = DateTime.Now.AddSeconds(bgAfter / 1000);
            if (sleepTask != null && !sleepTask.IsCompleted) return;

            sleepTask = Task.Factory.StartNew(() =>
            {
                while (bgTime > DateTime.Now)
                {
                    Thread.Sleep(bgAfter / 4);
                }
                lcdApp.PushToBackground();
            });
        }

        private static System.Drawing.Image ResizeImage(System.Drawing.Image imgToResize, Size size)
        {
            //Get the image current width  
            int sourceWidth = imgToResize.Width;
            //Get the image current height  
            int sourceHeight = imgToResize.Height;

            float nPercent;
            float nPercentW;
            float nPercentH;

            //Calulate  width with new desired size  
            nPercentW = ((float)size.Width / (float)sourceWidth);
            //Calculate height with new desired size  
            nPercentH = ((float)size.Height / (float)sourceHeight);
            if (nPercentH < nPercentW)
                nPercent = nPercentW;
            else
                nPercent = nPercentH;
            //New Width  
            int destWidth = (int)(sourceWidth * nPercent);
            //New Height  
            int destHeight = (int)(sourceHeight * nPercent);
            Bitmap b = new Bitmap(destWidth, destHeight);
            Graphics g = Graphics.FromImage((System.Drawing.Image)b);
            g.InterpolationMode = InterpolationMode.High;
            // Draw image with new width and height  
            g.DrawImage(imgToResize, 0, 0, destWidth, destHeight);
            g.Dispose();
            return (System.Drawing.Image)b;
        }
    }
}
