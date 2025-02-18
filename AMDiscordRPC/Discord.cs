using DiscordRPC;
using System;
using static AMDiscordRPC.Globals;

namespace AMDiscordRPC
{
    public class Discord
    {


        public static void InitializeDiscordRPC()
        {
            client = new DiscordRpcClient("1308911584164319282");
            client.Initialize();
        }

        public static void ChangeTimestamps(DateTime start = new DateTime(), DateTime end = new DateTime())
        {
            oldData.Timestamps = new Timestamps()
            {
                Start = (start != new DateTime()) ? start : oldData.Timestamps.Start,
                End = (end != new DateTime()) ? end : oldData.Timestamps.End
            };

            client.SetPresence(oldData);
        }

        public static void SetPresence(SongData x, string[] resp)
        {
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
