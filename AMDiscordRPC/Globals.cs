using AngleSharp.Html.Parser;
using DiscordRPC;
using DiscordRPC.Helper;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Filter;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Newtonsoft.Json.Linq;
using static AMDiscordRPC.Database;
using static AMDiscordRPC.UI;
using static AMDiscordRPC.Covers;

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
        public static SQLRPCResponse httpRes = new SQLRPCResponse();
        public static string ffmpegPath;
        public static S3_Creds S3_Credentials;
        public static AudioFormat format = AudioFormat.AAC;
        private static List<string> newMatchesArr;
        public static S3ConnectionStatus S3Status = S3ConnectionStatus.Disconnected;
        public static string AMRegion;
        public static readonly string baseOrganizationURL = "https://github.com/Penombra";
        public static SmallImage SelectedSmallImage = SmallImage.LossDolby;
        public static CloudflareTypes.AccountCredentials CfAccountCredentials = new CloudflareTypes.AccountCredentials(
            "",
            "",
            ""
        );
        public static IEnumerable<string> playButtons;
        public static IEnumerable<string> pauseButtons;
        
        public static void ConfigureLogger()
        {
            using (var stream = assembly.GetManifestResourceStream(typeof(AMDiscordRPC), "log4netconf.xml"))
            {
                XmlConfigurator.Configure(stream);
            }

            LevelRangeFilter lrf = new LevelRangeFilter
            {
                LevelMax = Level.Fatal,
                LevelMin = Level.Info
            };
#if DEBUG
            lrf.LevelMin = Level.Debug;
#endif
            lrf.ActivateOptions();

            PatternLayout pl = new PatternLayout
            {
                ConversionPattern = "[%date{HH:mm:ss.fff}] %level (%method:%line) - %message%newline"
            };
            pl.ActivateOptions();

            RollingFileAppender rfa = new RollingFileAppender
            {
                AppendToFile = false,
                File = @"logs/latest.log",
                Layout = pl,
            };
            rfa.AddFilter(lrf);
            rfa.ActivateOptions();

            ((Hierarchy)LogManager.GetRepository()).Root.AddAppender(rfa);
        }

        public static void InitRegion()
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
                log.Info($"Region selected as: {AMRegion.ToLower()}");
            }
            catch (Exception e)
            {
                log.Error($"Error happened while trying to select region, falling back to US Apple Music. Cause: {e}");
                AMRegion = "us";
            }
        }

        public static async void InitButtons()
        {
            try
            {
                HttpResponseMessage r =
                    await hclient.GetAsync($"{baseOrganizationURL}/AMScraper/releases/latest");
                if (r.IsSuccessStatusCode)
                {
                    HttpResponseMessage response =
                        await hclient.GetAsync(
                            $"{r.RequestMessage.RequestUri}/ButtonValues.json".Replace("tag", "download"));
                    if (response.IsSuccessStatusCode)
                    {
                        JObject values = JObject.Parse(await response.Content.ReadAsStringAsync());
                        playButtons = values["playButtons"].Values<string>();
                        pauseButtons = values["pauseButtons"].Values<string>();
                    }
                    else
                    {
                        Fallback();
                    }
                }
                else
                {
                    Fallback();
                }
            }
            catch (Exception e)
            {
                Fallback();
            }
            void Fallback()
            {
                log.Debug("Fallback to local values.");
                JObject values = JObject.Parse(Properties.Resources.ButtonValues);
                playButtons = values["playButtons"].Values<string>();
                pauseButtons = values["pauseButtons"].Values<string>();
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

        public static void ConfigureFromDB()
        {
            using (SQLiteDataReader dbResp = ExecuteReaderCommand($"SELECT {string.Join(", ", Regex.Matches(sqlMap["creds"], @"S3_\w+").FilterRepeatMatches())} FROM creds LIMIT 1"))
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
            SelectedSmallImage = (SmallImage)Convert.ToInt32(ExecuteScalarCommand("SELECT smallImage FROM clientSettings"));
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

        public enum SmallImage
        {
            LossDolby,
            Artist,
            None
        }

        public enum SecondaryType
        {
            EP,
            Album,
            Single,
            MV
        }

        public enum GWLP
        {
            EXSTYLE = -20,
            HINSTANCE = -6,
            HWNDPARENT = -8,
            ID = -12,
            STYLE = -16,
            USERDATA = -21,
            WNDPROC = -4
        }

        public enum WS : long
        {
            BORDER = 0x00800000L,
            CAPTION = 0x00C00000L,
            CHILD = 0x40000000L,
            CHILDWINDOW = 0x40000000L,
            CLIPCHILDREN = 0x02000000L,
            CLIPSIBLINGS = 0x04000000L,
            DISABLED = 0x08000000L,
            DLGFRAME = 0x00400000L,
            GROUP = 0x00020000L,
            HSCROLL = 0x00100000L,
            ICONIC = 0x20000000L,
            MAXIMIZE = 0x01000000L,
            MAXIMIZEBOX = 0x00010000L,
            MINIMIZE = 0x20000000L,
            MINIMIZEBOX = 0x00020000L,
            OVERLAPPED = 0x00000000L,
            OVERLAPPEDWINDOW = (OVERLAPPED | CAPTION | SYSMENU | THICKFRAME | MINIMIZEBOX | MAXIMIZEBOX),
            POPUP = 0x80000000L,
            POPUPWINDOW = (POPUP | BORDER | SYSMENU),
            SIZEBOX = 0x00040000L,
            SYSMENU = 0x00080000L,
            TABSTOP = 0x00010000L,
            THICKFRAME = 0x00040000L,
            TILED = 0x00000000L,
            TILEDWINDOW = (OVERLAPPED | CAPTION | SYSMENU | THICKFRAME | MINIMIZEBOX | MAXIMIZEBOX),
            VISIBLE = 0x10000000L,
            VSCROLL = 0x00200000L
        }

        public enum WS_EX : long
        {
            ACCEPTFILES = 0x00000010L,
            APPWINDOW = 0x00040000L,
            CLIENTEDGE = 0x00000200L,
            COMPOSITED = 0x02000000L,
            CONTEXTHELP = 0x00000400L,
            CONTROLPARENT = 0x00010000L,
            DLGMODALFRAME = 0x00000001L,
            LAYERED = 0x00080000L,
            LAYOUTRTL = 0x00400000L,
            LEFT = 0x00000000L,
            LEFTSCROLLBAR = 0x00004000L,
            LTRREADING = 0x00000000L,
            MDICHILD = 0x00000040L,
            NOACTIVATE = 0x08000000L,
            NOINHERITLAYOUT = 0x00100000L,
            NOPARENTNOTIFY = 0x00000004L,
            NOREDIRECTIONBITMAP = 0x00200000L,
            OVERLAPPEDWINDOW = (WINDOWEDGE | CLIENTEDGE),
            PALETTEWINDOW = (WINDOWEDGE | TOOLWINDOW | TOPMOST),
            RIGHT = 0x00001000L,
            RIGHTSCROLLBAR = 0x00000000L,
            RTLREADING = 0x00002000L,
            STATICEDGE = 0x00020000L,
            TOOLWINDOW = 0x00000080L,
            TOPMOST = 0x00000008L,
            TRANSPARENT = 0x00000020L,
            WINDOWEDGE = 0x00000100L
        }

        public enum HWND
        {
            BOTTOM = 1,
            NOTOPMOST = -2,
            TOP = 0,
            TOPMOST = -1
        }

        public enum SWP
        {
            ASYNCWINDOWPOS = 0x4000,
            DEFERERASE = 0x2000,
            DRAWFRAME = 0x0020,
            FRAMECHANGED = 0x0020,
            HIDEWINDOW = 0x0080,
            NOACTIVATE = 0x0010,
            NOCOPYBITS = 0x0100,
            NOMOVE = 0x0002,
            NOOWNERZORDER = 0x0200,
            NOREDRAW = 0x0008,
            NOREPOSITION = 0x0200,
            NOSENDCHANGING = 0x0400,
            NOSIZE = 0x0001,
            NOZORDER = 0x0004,
            SHOWWINDOW = 0x0040
        }

        public class SongData : EventArgs
        {
            public string SongName { get; set; }
            public string ArtistandAlbumName { get; set; }
            public bool IsMV { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public AudioFormat format { get; set; }

            public bool isSingle { get; set; }

            public SongData(string SongName, string ArtistandAlbumName, bool IsMV, DateTime StartTime, DateTime EndTime, AudioFormat format, bool isSingle)
            {
                this.SongName = SongName;
                this.ArtistandAlbumName = ArtistandAlbumName;
                this.IsMV = IsMV;
                this.StartTime = StartTime;
                this.EndTime = EndTime;
                this.format = format;
                this.isSingle = isSingle;
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

        public class AppleMusicScrapedData
        {
            public string[] Artists { get; set; }
            public string SongName { get; set; }
            public string AlbumName { get; set; }

            public SecondaryType? Type { get; set; }

            public AppleMusicScrapedData(string Artists = null, string SongName = null, string AlbumName = null, SecondaryType? Type = null)
            {
                this.Artists = Artists.ConvertArtistArray();
                this.SongName = SongName;
                this.AlbumName = AlbumName;
                this.Type = Type;
            }

            public string GetSearchString()
            {
                return Uri.EscapeDataString($"{SongName} {String.Join(", ", Artists)} — {AlbumName} - {Type.ToString()}");
            }
        }

        public class WebSongResponse
        {
            public string artworkURL { get; set; }
            public string trackURL { get; set; }
            public string trackName { get; set; }
            public string artistURL { get; set; }

            public WebSongResponse(string artworkURL = null, string trackURL = null, string trackName = null, string artistURL = null)
            {
                this.artworkURL = artworkURL;
                this.trackURL = trackURL;
                this.trackName = trackName;
                this.artistURL = artistURL;
            }

            public override bool Equals(object obj)
            {
                return obj is WebSongResponse other &&
                       artworkURL == other.artworkURL &&
                       trackURL == other.trackURL &&
                       trackName == other.trackName &&
                       artistURL == other.artistURL;
            }
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public class CloudflareTypes
        {
            public static readonly string endpointV4 = "https://api.cloudflare.com/client/v4";

            public enum Jurisdiction
            {
                [EnumMember(Value = "default")]
                Default = 0,

                [EnumMember(Value = "eu")]
                EU = 1,

                [EnumMember(Value = "fedramp")]
                FedRAMP = 2
            }

            public enum Location
            {
                [EnumMember(Value = "apac")]
                APAC,

                [EnumMember(Value = "eeur")]
                EEUR,

                [EnumMember(Value = "enam")]
                ENAM,

                [EnumMember(Value = "weur")]
                WEUR,

                [EnumMember(Value = "wnam")]
                WNAM,

                [EnumMember(Value = "oc")]
                OC
            }

            public enum StorageClass
            {
                [EnumMember(Value = "standard")]
                Standard,

                [EnumMember(Value = "infrequent_access")]
                InfrequentAccess
            }

            public class ResponseInfo
            {
                public int code { get; set; }
                public string message { get; set; }
                public string documentation_url { get; set; }
                public Source source { get; set; }

                public ResponseInfo(int code, string message, string documentation_url, Source source)
                {
                    this.code = code;
                    this.message = message;
                    this.documentation_url = documentation_url;
                    this.source = source;
                }
            }

            public class Response
            {
                public List<ResponseInfo> errors { get; set; }
                public dynamic messages { get; set; }
                public dynamic result { get; set; }
                public bool success { get; set; }

                public Response(List<ResponseInfo> errors, dynamic messages, dynamic result, bool success)
                {
                    this.errors = errors;
                    this.messages = messages;
                    this.result = result;
                    this.success = success;
                }
            }

            public class Source
            {
                public string pointer { get; set; }
            }

            public class Bucket
            {
                public DateTime creation_date { get; set; }
                public Jurisdiction jurisdiction { get; set; }
                public Location location { get; set; }
                public string name { get; set; }
                public StorageClass storage_class { get; set; }

                [JsonConstructor]
                public Bucket(DateTime creation_date, Jurisdiction jurisdiction, Location location, string name, StorageClass storage_class)
                {
                    this.creation_date = creation_date;
                    this.jurisdiction = jurisdiction;
                    this.location = location;
                    this.name = name;
                    this.storage_class = storage_class;
                }
            }

            public class AccountCredentials
            {
                public string api_token { get; set; }
                public string zone_id { get; set; }
                public string account_id { get; set; }

                public AccountCredentials(string api_token, string zone_id, string account_id)
                {
                    this.api_token = api_token;
                    this.zone_id = zone_id;
                    this.account_id = account_id;
                }
            }
        }

        public static String TrimEnd(this String str, int count)
        {
            return str.Substring(0, str.Length - count);
        }

        public static String[] ConvertArtistArray(this String str)
        {
            // I'll be adding database that has special artists whose nickname includes " & " or ", " in next update (AFTER THE PROJECT TRANSFER TO ORGANIZATION ACCOUNT) 
            List<string> extractedValues = new List<string>();
            foreach (string value in str.Split(new string[] { ", " }, StringSplitOptions.None))
            {
                if (value.Contains(" & "))
                {
                    extractedValues.AddRange(value.Split(new string[] { " & " }, StringSplitOptions.None));
                }
                else extractedValues.Add(value);
            }
            return extractedValues.ToArray();
        }

        public static long? SemanticPack(this string version)
        {
            string[] str = version.Split('.');
            if (str.Length == 3)
            {
                return (long.Parse(str[0]) << 32) | (long.Parse(str[1]) << 16) | uint.Parse(str[2]);
            }
            return null;
        }
        
        public static string SemanticUnpack(this long version)
        {
            if (version >= 4294967296)
            {
                return $"{version >> 32}.{version >> 16 & 0xFFFF}.{version & 0xFFFF}";
            }
            return null;
        }
        public static String[] GetArtist(this IElement element, AppleMusicScrapedData data)
        {
            try
            {
                string[] returnData = new string[3];
                foreach (IElement innerElement in element.QuerySelectorAll("div.ellipse-lockup-wrapper"))
                {
                    string title = innerElement.QuerySelector("h3.title").TextContent;
                    if (data.Artists[0].Equals(title))
                    {
                        returnData[0] = innerElement.QuerySelector("a.click-action").GetAttribute("href");
                        returnData[1] = innerElement.QuerySelector("source[type=\"image/jpeg\"]").GetAttribute("srcset")
                            .Split(' ')[0];
                        returnData[2] = data.Artists[data.Artists.IndexOf(title)];
                        return returnData;
                    }
                    else if (returnData[0] == null && data.Artists.Contains(title))
                    {
                        returnData[0] = innerElement.QuerySelector("a.click-action").GetAttribute("href");
                        returnData[1] = innerElement.QuerySelector("source[type=\"image/jpeg\"]").GetAttribute("srcset")
                            .Split(' ')[0];
                        returnData[2] = data.Artists[data.Artists.IndexOf(title)];
                    }
                }
                if (returnData[0] == null)
                {
                    foreach (IElement slowElement in element.QuerySelectorAll(@"ul.track-lockup__content"))
                    {
                        IHtmlCollection<IElement> texts = slowElement.QuerySelectorAll(@"div.track-lockup__clamp-wrapper");
                        if (texts[0].NormalizedText().Equals(data.SongName, StringComparison.OrdinalIgnoreCase) &&
                            data.Artists.Contains(texts[1].QuerySelector("span").NormalizedText()))
                        {
                            returnData[0] = texts[1].QuerySelector("a").GetAttribute("href");
                            returnData[1] = AsyncArtistProfileFetch(returnData[0]).Result;
                            returnData[2] = data.Artists[data.Artists.IndexOf(texts[1].QuerySelector("span").NormalizedText())];
                            return returnData;
                        }
                    }
                }
                return (returnData[0] == null) ? null : returnData;
            }
            catch (Exception e)
            {
                log.Error($"Something has changed in Apple Music website that causes this error please report to the developer. GetArtist: {e.Message}");
            }
            return null;
        }
        
        public static String GetAlbum(this IElement element, AppleMusicScrapedData data)
        {
            try
            {
                string foundAlbum = null;
                foreach (IElement innerElement in element.QuerySelector(@"div[aria-label=""Albums""]")
                             .QuerySelectorAll("div > div > section > div > ul > li"))
                {
                    IHtmlCollection<IElement> texts = innerElement.QuerySelectorAll("span.multiline-clamp__text > a");
                    if (texts.Length > 1)
                    {
                        if (texts[0].NormalizedText().Equals(data.AlbumName, StringComparison.OrdinalIgnoreCase) &&
                            data.Artists.Contains(texts[1].NormalizedText()))
                        {
                            return texts[0].GetAttribute("href");
                        }
                        else if (texts[0].NormalizedText().Contains(data.AlbumName, StringComparison.OrdinalIgnoreCase) &&
                                 data.Artists.Contains(texts[1].NormalizedText()))
                        {
                            foundAlbum = texts[0].GetAttribute("href");
                        }
                    }
                }
                return foundAlbum;
            }
            catch (Exception e)
            {
                log.Error($"Something has changed in Apple Music website that causes this error please report to the developer. GetAlbum: {e.Message}");
            }
            return null;
        }

        public static String NormalizedText(this INode node)
        {
            return node.TextContent.Replace("  ", " ").Trim();
        }
        
        public static String GetSong(this IElement element, AppleMusicScrapedData data, string cover)
        {
            try
            {
                // IDK why but some songs are just impossible to detect without second web request. This ones does it's best to detect within the first request so some songs can be shown under different album.
                // Example: https://music.apple.com/tr/album/born-again-feat-doja-cat-raye/1794222371?i=1794222374 this exact song is also in this https://music.apple.com/tr/album/born-again-feat-doja-cat-raye/1799777820?i=1799777825 album with exact same cover and name. Since song section doesn't have album data we can't determine if it's the same album without second request.
                string poppedCover = (cover != null)
                    ? String.Join("/", cover.Split('/').Take(cover.Split('/').Length - 1))
                    : null;
                string foundSong = null;
                
                foreach (IElement innerElement in element.QuerySelectorAll(@"div.track-lockup"))
                {
                    IHtmlCollection<IElement> texts = innerElement.QuerySelectorAll(@"div.track-lockup__clamp-wrapper");
                    string coverURL = innerElement.QuerySelectorAll("div > div > div > picture > source")[1]
                        .GetAttribute("srcset")
                        .Split(',')[1].Split(' ')[0];
                    if (texts[0].NormalizedText().Equals(data.SongName, StringComparison.OrdinalIgnoreCase) &&
                        data.Artists.Contains(texts[1].QuerySelector("span").NormalizedText())
                        && coverURL.Contains(poppedCover))
                    {
                        return texts[0].QuerySelector("a").GetAttribute("href");
                    }
                    else if (texts[0].NormalizedText().Equals(data.SongName, StringComparison.OrdinalIgnoreCase) &&
                             data.Artists.Contains(texts[1].QuerySelector("span").NormalizedText()))
                    {
                        foundSong = texts[0].QuerySelector("a").GetAttribute("href");
                    }
                }
                return foundSong;
            }
            catch (Exception e)
            {
                log.Error($"Something has changed in Apple Music website that causes this error please report to the developer. GetSong: {e.Message}");
            }
            return null;
        }
        
        public static String GetCover(this IElement element, AppleMusicScrapedData data)
        {
            try
            {
                string foundCover = null;
                foreach (IElement innerElement in element.QuerySelector(@"div[aria-label=""Albums""]")
                             .QuerySelectorAll("div > div > section > div > ul > li"))
                {
                        // This requires its own try-catch because Apple Music's website has invisible albums with no data to load later and it just throws an error when it gets there.
                    IHtmlCollection<IElement> texts =
                        innerElement.QuerySelectorAll("span.multiline-clamp__text > a");
                    if (texts.Length > 1)
                    {
                        if (texts[0].NormalizedText().Equals(data.AlbumName, StringComparison.OrdinalIgnoreCase) &&
                            data.Artists.Contains(texts[1].NormalizedText()))
                        {
                            return innerElement.QuerySelectorAll("div > div > div > picture > source")[1]
                                .GetAttribute("srcset")
                                .Split(',')[1].Split(' ')[0];
                        }
                        else if (texts[0].NormalizedText().Contains(data.AlbumName, StringComparison.OrdinalIgnoreCase) &&
                                 data.Artists.Contains(texts[1].NormalizedText()))
                        {
                            foundCover = innerElement.QuerySelectorAll("div > div > div > picture > source")[1]
                                .GetAttribute("srcset")
                                .Split(',')[1].Split(' ')[0];
                        }
                    }
                }
                if (foundCover != null)
                {
                    return foundCover;
                }
                foreach (IElement innerElement in element.QuerySelectorAll("div.track-lockup"))
                {
                    if (data.SongName.Contains(innerElement.QuerySelector("ul > li > div > a").NormalizedText()) &&
                        data.Artists.Contains(innerElement.QuerySelector("ul > li > div > span > a > span")
                            .NormalizedText()))
                    {
                        return innerElement.QuerySelectorAll("div > div > div > picture > source")[1]
                            .GetAttribute("srcset")
                            .Split(',')[1].Split(' ')[0];
                    }
                }
            }
            catch (Exception e)
            {
                log.Error($"Something has changed in Apple Music website that causes this error please report to the developer. GetCover: {e.Message}");
            }
            return null;
        }
    }
}