﻿using DiscordRPC;
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
        public static readonly ILog log = LogManager.GetLogger(typeof(AMDiscordRPC));
        public static readonly Assembly assembly = Assembly.GetExecutingAssembly();

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
            data.Replace("—", "-");
            if (!data.WithinLength(128, Encoding.UTF8))
            {
                byte[] byteArr = Encoding.UTF8.GetBytes(data);
                Array.Resize(ref byteArr, 128);
                data = Encoding.UTF8.GetString(byteArr).TrimEnd('�');
            }
            return data;
        }

        public class SongData : EventArgs
        {
            public string SongName { get; set; }
            public string ArtistandAlbumName { get; set; }
            public bool isMV { get; set;}

            public SongData(string songName, string artistandAlbumName, bool isMVl)
            {
                SongName = songName;
                ArtistandAlbumName = artistandAlbumName;
                isMV = isMVl;
            }
        }
    }
}
