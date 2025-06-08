using AngleSharp.Html.Parser;
using DiscordRPC;
using DiscordRPC.Helper;
using log4net;
using log4net.Config;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
        public static string ffmpegPath;
        public static S3_Creds S3_Credentials;

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
            public static void ChangeSong(SongData e)
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

        public static void InitDBCreds()
        {
            using (SQLiteDataReader dbResp = Database.ExecuteReaderCommand("SELECT * FROM creds LIMIT 1"))
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

        private static void StartFFMpegProcess(string filename)
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
                log.Error($"FFMpeg Check error: {ex}");
            }
        }

        public static async void CheckFFMpeg()
        {
            List<string> paths = Environment.GetEnvironmentVariable("PATH").Split(';').Where(v => v.Contains("ffmpeg")).Select(s => $@"{s}\ffmpeg.exe").Prepend("ffmpeg").ToList();
            foreach (var item in paths)
            {
                StartFFMpegProcess(item);
                if (ffmpegPath != null)
                {
                    break;
                }
            }
            if (ffmpegPath != null)
            {
                log.Info($"Found ffmpeg");
            }
            else log.Warn("FFmpeg not found");
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
    }
}
