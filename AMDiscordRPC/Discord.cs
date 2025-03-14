﻿using DiscordRPC;
using System;
using System.Threading.Tasks;
using System.Web;
using static AMDiscordRPC.Globals;
using static AMDiscordRPC.Covers;
using System.Threading;

namespace AMDiscordRPC
{
    public class Discord
    {
        private static Thread thread;

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
            log.Debug($"Timestamps {x.startTime}/{x.endTime}");
            if (thread != null) thread.Abort();
            oldData.Details = ConvertToValidString(x.SongName);
            oldData.Timestamps = new Timestamps()
            {
                Start = x.startTime,
                End = x.endTime,
            };
            client.SetPresence(oldData);
            Task t = Task.Run(async () => {
                thread = Thread.CurrentThread;
                await AsyncSetButton(x);
            });
        }

        private static async Task AsyncSetButton(SongData x)
        {
            string[] resp = await FetchiTunes(HttpUtility.UrlEncode(ConvertToValidString(x.ArtistandAlbumName) + $" {ConvertToValidString(x.SongName)}"));
            oldData.Buttons = new Button[]
            {
                new Button() { Label = "Listen on Apple Music", Url = (resp.Length > 0) ? resp[1].Replace("https://", "music://") : "music://music.apple.com/home"}
            };
            client.SetPresence(oldData);
            thread = null;
        }

        public static void SetPresence(SongData x, string[] resp)
        {
            log.Debug($"Timestamps {x.startTime}/{x.endTime}");
            if (thread != null) thread.Abort();
            oldData = new RichPresence()
            {
                Type = ActivityType.Listening,
                Details = ConvertToValidString(x.SongName),
                State = $"by {((x.isMV) ? x.ArtistandAlbumName : ConvertToValidString(x.ArtistandAlbumName.Split('—')[0]))}",
                Assets = new Assets()
                {
                    LargeImageKey = (resp.Length > 0) ? resp[0] : "",
                    LargeImageText = (x.isMV) ? resp[2] : ConvertToValidString(x.ArtistandAlbumName.Split('—')[1]),
                },
                Buttons = new Button[]
                     {
                         new Button() { Label = "Listen on Apple Music", Url = (resp.Length > 0) ? resp[1].Replace("https://", "music://") : "music://music.apple.com/home"}
                     },
                Timestamps = new Timestamps()
                {
                    Start = x.startTime,
                    End = x.endTime,
                }
            };
            client.SetPresence(oldData);
        }
    }
}
