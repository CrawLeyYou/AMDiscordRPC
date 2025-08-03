using AngleSharp.Html.Parser;
using DiscordRPC;
using DiscordRPC.Helper;
using log4net;
using log4net.Config;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using static AMDiscordRPC.Database;
using static AMDiscordRPC.UI;

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
        public static WebSongResponse httpRes = new WebSongResponse();
        public static string ffmpegPath;
        public static S3_Creds S3_Credentials;
        private static List<string> newMatchesArr;
        public enum S3ConnectionStatus
        {
            Connected,
            Disconnected,
            Error
        }
        public enum AudioFormat
        {
            Lossless,
            Dolby_Atmos,
            Dolby_Audio,
            AAC
        }
        public static S3ConnectionStatus S3Status = S3ConnectionStatus.Disconnected;
        public static string AMRegion;


        public static void ConfigureLogger()
        {
            using (var stream = assembly.GetManifestResourceStream(typeof(AMDiscordRPC), "log4netconf.xml"))
            {
                XmlConfigurator.Configure(stream);
            }
        }

        public static async void InitRegion()
        {
            HttpClientHandler HClientHandlerhandler = new HttpClientHandler();
            CookieContainer cookies = new CookieContainer();
            HClientHandlerhandler.CookieContainer = cookies;
            HttpClient httpClient = new HttpClient(HClientHandlerhandler);

            try
            {
                _ = httpClient.GetAsync("https://music.apple.com/").Result;

                AMRegion = cookies.GetCookies(new Uri("https://music.apple.com/")).Cast<Cookie>()
                    .Where(cookie => cookie.Name == "geo").ToList()[0].Value;
            }
            catch (Exception e)
            {
                log.Error($"Error happened while trying to select region, falling back to US Apple Music. Cause: {e}");
                AMRegion = "US";
            }
        }

        public class AMSongDataEvent
        {
            public static event EventHandler<SongData> SongChanged;
            public static void ChangeSong(SongData e)
            {
                SongChanged?.Invoke(null, e);
            }
        }

        public static string ConvertToValidString(string data)
        {
            //We dont need byte validation anymore because of this fix https://github.com/Lachee/discord-rpc-csharp/pull/259. Going to change this method soon.
            if (!data.WithinLength(125, Encoding.UTF8))
            {
                byte[] byteArr = Encoding.UTF8.GetBytes(data);
                Array.Resize(ref byteArr, 125);
                data = Encoding.UTF8.GetString(byteArr).TrimEnd('�');
            }
            return data;
        }

        public static void InitDBCreds()
        {
            using (SQLiteDataReader dbResp = Database.ExecuteReaderCommand($"SELECT {string.Join(", ", Regex.Matches(Database.sqlMap["creds"], @"S3_\w+").FilterRepeatMatches())} FROM creds LIMIT 1"))
            {
                while (dbResp.Read())
                {
                    S3_Credentials = new S3_Creds(
                        ((!dbResp.IsDBNull(0)) ? dbResp.GetString(0) : null),
                        ((!dbResp.IsDBNull(1)) ? dbResp.GetString(1) : null),
                        ((!dbResp.IsDBNull(2)) ? dbResp.GetString(2) : null),
                        ((!dbResp.IsDBNull(3)) ? dbResp.GetString(3) : null),
                        ((!dbResp.IsDBNull(4)) ? dbResp.GetString(4) : null),
                        ((!dbResp.IsDBNull(5)) ? dbResp.GetBoolean(5) : null));
                }
            }
        }

        private static void StartFFmpegProcess(string filename)
        {
            try
            {
                Process proc = new Process();
                proc.StartInfo.FileName = filename;
                proc.StartInfo.Arguments = "-version";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.CreateNoWindow = true;
                proc.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    if (!String.IsNullOrEmpty(e.Data))
                    {
                        if (e.Data.Contains("ffmpeg"))
                        {
                            ffmpegPath = filename;
                        }
                    }
                });

                proc.Start();
                proc.BeginOutputReadLine();
                proc.WaitForExit();
            }
            catch (Exception ex)
            {
                log.Error($"FFmpeg Check error: {ex}");
            }
        }

        public static async void CheckFFmpeg()
        {
            List<string> paths = Environment.GetEnvironmentVariable("PATH").Split(';').Where(v => v.Contains("ffmpeg")).Select(s => $@"{s}\ffmpeg.exe").Prepend("ffmpeg").ToList();
            object SQLQueryRes = ExecuteScalarCommand($"SELECT FFmpegPath from creds");

            if (SQLQueryRes != null)
            {
                paths.Add(SQLQueryRes.ToString() + "\\ffmpeg.exe");
            }
            foreach (string item in paths)
            {
                StartFFmpegProcess(item);
                if (ffmpegPath != null)
                {
                    break;
                }
            }
            if (ffmpegPath != null)
            {
                log.Info($"Found ffmpeg");
            }
            else FFmpegDialog();
        }

        public class SongData : EventArgs
        {
            public string SongName { get; set; }
            public string ArtistandAlbumName { get; set; }
            public bool IsMV { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public AudioFormat format { get; set; }

            public SongData(string SongName, string ArtistandAlbumName, bool IsMV, DateTime StartTime, DateTime EndTime, AudioFormat format)
            {
                this.SongName = SongName;
                this.ArtistandAlbumName = ArtistandAlbumName;
                this.IsMV = IsMV;
                this.StartTime = StartTime;
                this.EndTime = EndTime;
                this.format = format;
            }
        }

        public static List<string> FilterRepeatMatches(this MatchCollection matches)
        {
            return matches.Cast<Match>().Select(m => m.Value).Distinct().ToList();
        }

        public class S3_Creds
        {
            public string accessKey { get; set; }
            public string secretKey { get; set; }
            public string serviceURL { get; set; }
            public string bucketName { get; set; }
            public string bucketURL { get; set; }
            public bool? isSpecificKey { get; set; }

            public S3_Creds(string accessKey, string secretKey, string serviceURL, string bucketName, string bucketURL, bool? isSpecificKey)
            {
                this.accessKey = accessKey;
                this.secretKey = secretKey;
                this.serviceURL = serviceURL;
                this.bucketName = bucketName;
                this.bucketURL = bucketURL;
                this.isSpecificKey = isSpecificKey;
            }

            public List<string> GetNullKeys()
            {
                return GetType().GetProperties().Where(s => s.GetValue(this) == null).Select(p => p.Name).ToList();
            }

            public List<string> GetNotNullKeys()
            {
                return GetType().GetProperties().Where(s => s.GetValue(this) != null).Select(p => $"S3_{p.Name}").ToList();
            }

            public List<object> GetNotNullValues()
            {
                return GetType().GetProperties().Where(s => s.GetValue(this) != null).Select(p => (p.PropertyType == typeof(string)) ? $"'{p.GetValue(this)}'" : p.GetValue(this)).ToList();
            }
        }

        public class WebSongResponse
        {
            public string artworkURL { get; set; }
            public string trackURL { get; set; }
            public string trackName { get; set; }

            public WebSongResponse(string artworkURL = null, string trackURL = null, string trackName = null)
            {
                this.artworkURL = artworkURL;
                this.trackURL = trackURL;
                this.trackName = trackName;
            }

            public override bool Equals(object obj)
            {
                return obj is WebSongResponse other &&
                       artworkURL == other.artworkURL &&
                       trackURL == other.trackURL &&
                       trackName == other.trackName;
            }
        }
    }
}