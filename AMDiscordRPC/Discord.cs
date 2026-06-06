using DiscordRPC;
using System;
using System.Threading;
using System.Threading.Tasks;
using static AMDiscordRPC.Covers;
using static AMDiscordRPC.Database;
using static AMDiscordRPC.Globals;

namespace AMDiscordRPC
{
    public class Discord
    {
        private static Thread thread = null;
        public static CancellationTokenSource animatedCoverCts;

        public static void InitDiscordRPC()
        {
            client = new DiscordRpcClient("1308911584164319282");
            client.Initialize();
            log.Debug("Discord RPC initialized.");
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

        public static void ChangeSmallImage(SmallImage x)
        {
            switch (x)
            {
                case SmallImage.None:
                    oldData.Assets.SmallImageKey = null;
                    client.SetPresence(oldData);
                    break;
                case SmallImage.LossDolby:
                    oldData.Assets.SmallImageKey = (format == AudioFormat.Lossless) ? "lossless" :
                        (format == AudioFormat.Dolby_Atmos || format == AudioFormat.Dolby_Audio) ? "dolbysimplified" :
                        null;
                    oldData.Assets.SmallImageText = (format == AudioFormat.Lossless) ? "Lossless" :
                        (format == AudioFormat.Dolby_Atmos) ? "Dolby Atmos" :
                        (format == AudioFormat.Dolby_Audio) ? "Dolby Audio" : null;
                    oldData.Assets.SmallImageUrl = null;
                    client.SetPresence(oldData);
                    break;
                case SmallImage.Artist:
                    oldData.Assets.SmallImageKey = httpRes.artistProfileSource;
                    oldData.Assets.SmallImageText = oldData.State;
                    oldData.Assets.SmallImageUrl = oldData.StateUrl;
                    client.SetPresence(oldData);
                    break;
            }
        }
        
        private static async Task AsyncSetButton(SongData x)
        {
            SQLRPCResponse resp = await GetCover(new AppleMusicScrapedData(
                x.ArtistandAlbumName.Split(new string[] { " — " }, StringSplitOptions.None)[0],
                x.SongName,
                x.ArtistandAlbumName.Split(new string[] { " — " }, StringSplitOptions.None)[1],
                (x.isSingle) ? SecondaryType.Single : (x.IsMV) ? SecondaryType.MV : (x.ArtistandAlbumName.Contains(" - EP") ? SecondaryType.EP : SecondaryType.Album)
            ));
            oldData.Buttons = new Button[]
            {
                new Button() { Label = "Listen on Apple Music", Url = (resp.songURL != null) ? resp.songURL.Replace("https://", "music://") : "music://music.apple.com/home"}
            };
            oldData.DetailsUrl = resp.songURL;
            client.SetPresence(oldData);
            thread = null;
        }

        public static async Task SetCover(string coverURL)
        {
            oldData.Assets.LargeImageKey = coverURL;
            client.SetPresence(oldData);
            animatedCoverCts = null;
        }

        public static void SetPresence(SongData x, SQLRPCResponse resp)
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
                DetailsUrl = resp.songURL,
                StateUrl = resp.artistRedirURL,
                StatusDisplay = StatusDisplayType.State,
                State = (x.IsMV) ? x.ArtistandAlbumName : ConvertToValidString(x.ArtistandAlbumName.Split('—')[0]),
                Assets = new Assets()
                {
                    LargeImageKey = (resp.coverURL != null) ? resp.coverURL : "",
                    LargeImageUrl = resp.albumURL,
                    LargeImageText = (x.IsMV && x.SongName != null) ? x.SongName : ConvertToValidString(x.ArtistandAlbumName.Split('—')[1]),
                    SmallImageKey = (SelectedSmallImage == SmallImage.Artist) ? resp.artistProfileSource : (x.format == AudioFormat.Lossless) ? "lossless" : (x.format == AudioFormat.Dolby_Atmos || x.format == AudioFormat.Dolby_Audio) ? "dolbysimplified" : null,
                    SmallImageText = (SelectedSmallImage == SmallImage.Artist) ? ConvertToValidString(x.ArtistandAlbumName.Split('—')[0]) : (x.format == AudioFormat.Lossless) ? "Lossless" : (x.format == AudioFormat.Dolby_Atmos) ? "Dolby Atmos" : (x.format == AudioFormat.Dolby_Audio) ? "Dolby Audio" : null,
                    SmallImageUrl = (SelectedSmallImage == SmallImage.Artist) ? resp.artistRedirURL : null
                },
                Buttons = new Button[]
                     {
                         new Button() { Label = "Listen on Apple Music", Url = (resp.songURL != null) ? resp.songURL.Replace("https://", "music://") : "music://music.apple.com/home"}
                     },
                Timestamps = new Timestamps()
                {
                    Start = x.StartTime,
                    End = x.EndTime,
                }
            };
            if (oldData.Assets.LargeImageText.Length == 1)
                oldData.Assets.LargeImageText = $"{oldData.Assets.LargeImageText}‍"; // THIS HAS U+200D AT THE END OF STRING TO FIX '"large_text" length must be at least 2 characters long' ERROR
            client.SetPresence(oldData);
            if (resp.coverURL != null && !resp.coverURL.Contains((S3_Credentials != null) ? (S3_Credentials.GetNullKeys().Count == 0) ? S3_Credentials.bucketURL : "" : ""))
            {
                animatedCoverCts = new CancellationTokenSource();
                Task t = new Task(() => CheckAnimatedCover(resp.albumURL, animatedCoverCts.Token));
                t.Start();
            }
        }
    }
}
