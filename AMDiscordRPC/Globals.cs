﻿using AngleSharp.Html.Parser;
using DiscordRPC;
using DiscordRPC.Helper;
using log4net;
using log4net.Config;
using System;
using System.Net.Http;
using System.Reflection;
using System.Text;

namespace AMDiscordRPC
{
    public static class Globals
    {
        public static DiscordRpcClient client;
        public static HttpClient hclient = new HttpClient();
        public static FlaUI.Core.Application AppleMusicProc;
        public static bool AMAttached;
        public static string localizedPlay;
        public static string localizedStop;
        public static readonly ILog log = LogManager.GetLogger(typeof(AMDiscordRPC));
        public static readonly Assembly assembly = Assembly.GetExecutingAssembly();
        public static HtmlParser parser = new HtmlParser();
        public static RichPresence oldData = new RichPresence();
        public static string[] httpRes = Array.Empty<string>();

        public static void ConfigureLogger()
        {
            using (var stream = assembly.GetManifestResourceStream(typeof(AMDiscordRPC), "log4netconf.xml"))
            {
                XmlConfigurator.Configure(stream);
            }
        }

        public class AMSongDataEvent
        {
            public static event EventHandler<SongData> SongChanged;
            public static void SongChange(SongData e)
            {
                SongChanged?.Invoke(null, e);
            }
        }

        public static string ConvertToValidString(string data)
        {
            if (!data.WithinLength(125, Encoding.UTF8))
            {
                byte[] byteArr = Encoding.UTF8.GetBytes(data);
                Array.Resize(ref byteArr, 125);
                data = Encoding.UTF8.GetString(byteArr).TrimEnd('�');
            }
            return data;
        }

        public class SongData : EventArgs
        {
            public string SongName { get; set; }
            public string ArtistandAlbumName { get; set; }
            public bool IsMV { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public int AudioDetail { get; set; }

            public SongData(string SongName, string ArtistandAlbumName, bool IsMV, DateTime StartTime, DateTime EndTime, int AudioDetail)
            {
                this.SongName = SongName;
                this.ArtistandAlbumName = ArtistandAlbumName;
                this.IsMV = IsMV;
                this.StartTime = StartTime;
                this.EndTime = EndTime;
                this.AudioDetail = AudioDetail;
            }
        }
    }
}
