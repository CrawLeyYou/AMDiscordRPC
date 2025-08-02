using DiscordRPC;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using static AMDiscordRPC.Covers;
using static AMDiscordRPC.Globals;

namespace AMDiscordRPC
{
    public class Discord
    {
        private static Thread thread = null;
        public static CancellationTokenSource animatedCoverCts;

        public static void InitializeDiscordRPC()
        {
            client = new DiscordRpcClient("1308911584164319282");
            client.Initialize();
        }

        public static void ChangeTimestamps(DateTime start = new DateTime(), DateTime end = new DateTime())
        {
            log.Debug($"Timestamps {start}/{end}");
            oldData.Timestamps = new Timestamps()
            {
                Start = (start != new DateTime()) ? start : oldData.Timestamps.Start,
                End = (end != new DateTime()) ? end : oldData.Timestamps.End
            };
            client.SetPresence(oldData);
        }

        public static void SetPresence(SongData x)
        {
            log.Debug($"Timestamps {x.StartTime}/{x.EndTime}");
            if (thread != null) thread.Abort();
            oldData.Details = ConvertToValidString(x.SongName);
            oldData.Timestamps = new Timestamps()
            {
                Start = x.StartTime,
                End = x.EndTime,
            };
            client.SetPresence(oldData);
            Task t = Task.Run(async () =>
            {
                thread = Thread.CurrentThread;
                await AsyncSetButton(x);
            });
        }

        private static async Task AsyncSetButton(SongData x)
        {
            WebSongResponse resp = await GetCover(x.ArtistandAlbumName.Split('—')[1], HttpUtility.UrlEncode(x.ArtistandAlbumName + $" {x.SongName}"));
            oldData.Buttons = new Button[]
            {
                new Button() { Label = "Listen on Apple Music", Url = (resp.trackURL != null) ? resp.trackURL.Replace("https://", "music://") : "music://music.apple.com/home"}
            };
            client.SetPresence(oldData);
            thread = null;
        }

        public static async Task SetCover(string coverURL)
        {
            oldData.Assets.LargeImageKey = coverURL;
            client.SetPresence(oldData);
            animatedCoverCts = null;
        }

        public static void SetPresence(SongData x, WebSongResponse resp)
        {
            log.Debug($"Timestamps {x.StartTime}/{x.EndTime}");
            if (thread != null) thread.Abort();
            if (animatedCoverCts != null)
            {
                animatedCoverCts.Cancel();
                animatedCoverCts.Dispose();
            }
            oldData = new RichPresence()
            {
                Type = ActivityType.Listening,
                Details = ConvertToValidString(x.SongName),
                StatusDisplay = StatusDisplayType.State,
                State = (x.IsMV) ? x.ArtistandAlbumName : ConvertToValidString(x.ArtistandAlbumName.Split('—')[0]),
                Assets = new Assets()
                {
                    LargeImageKey = (resp.artworkURL != null) ? resp.artworkURL : "",
                    LargeImageText = (x.IsMV && resp.trackName != null) ? resp.trackName : ConvertToValidString(x.ArtistandAlbumName.Split('—')[1]),
                    SmallImageKey = (x.format == AudioFormat.Lossless) ? "lossless" : (x.format == AudioFormat.Dolby_Atmos || x.format == AudioFormat.Dolby_Audio) ? "dolbysimplified" : null,
                    SmallImageText = (x.format == AudioFormat.Lossless) ? "Lossless" : (x.format == AudioFormat.Dolby_Atmos) ? "Dolby Atmos" : (x.format == AudioFormat.Dolby_Audio) ? "Dolby Audio" : null,
                },
                Buttons = new Button[]
                     {
                         new Button() { Label = "Listen on Apple Music", Url = (resp.trackURL != null) ? resp.trackURL.Replace("https://", "music://") : "music://music.apple.com/home"}
                     },
                Timestamps = new Timestamps()
                {
                    Start = x.StartTime,
                    End = x.EndTime,
                }
            };
            client.SetPresence(oldData);
            if (resp.artworkURL != null && !resp.artworkURL.Contains((S3_Credentials != null) ? (S3_Credentials.GetNullKeys().Count == 0) ? S3_Credentials.bucketURL : "" : ""))
            {
                animatedCoverCts = new CancellationTokenSource();
                Task t = new Task(() => CheckAnimatedCover(ConvertToValidString(x.ArtistandAlbumName.Split('—')[1]), resp.trackURL, animatedCoverCts.Token));
                t.Start();
            }
        }
    }
}
