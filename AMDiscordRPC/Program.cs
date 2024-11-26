using System;
using FlaUI.UIA3;
using System.Linq;
using System.Threading;
using DiscordRPC;
using System.Net.Http;
using System.Web;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3.Converters;

namespace AMDiscordRPC
{
    internal class Program
    {
        public static DiscordRpcClient client;
        public static HttpClient hclient = new HttpClient();
        public static FlaUI.Core.Application AppleMusic;
        public static bool AMAttached;
        public static string localizedPlay;

        static void Main(string[] args)
        {
            InitializeDiscordRPC();
            AttachToAppleMusic();
            AMSongDataEvent.SongChanged += async (sender, x) =>
             {
                 Console.WriteLine($"Song: {x.SongName}\nArtist and Album: {x.ArtistandAlbumName}");
                 string[] resp = await FetchiTunes(HttpUtility.UrlEncode(x.ArtistandAlbumName.Replace("—", "-") + $" {x.SongName}"));
                 bool isMV = (x.ArtistandAlbumName.Split('—').Length <= 1) ? true : false;
                 client.SetPresence(new RichPresence()
                 {
                     Type = ActivityType.Listening,
                     Details = x.SongName,
                     State = (isMV) ? x.ArtistandAlbumName : x.ArtistandAlbumName.Split('—')[0],
                     Assets = new Assets()
                     {
                         LargeImageKey = (resp.Length > 0) ? resp[0] : "",
                         LargeImageText = (isMV) ? x.ArtistandAlbumName : x.ArtistandAlbumName.Split('—')[0],
                     },
                     Buttons = new DiscordRPC.Button[]
                     {
                         new DiscordRPC.Button() { Label = "Listen on Apple Music", Url = (resp.Length > 0) ? resp[1].Replace("https://", "music://") : "music://music.apple.com/home"}
                     }
                 });
             };
            AMEvent();
        }

        static void InitializeDiscordRPC()
        {
            client = new DiscordRpcClient("1308911584164319282");
            client.Initialize();
        }

        static void AttachToAppleMusic()
        {
            try
            {
                AppleMusic = FlaUI.Core.Application.Attach("AppleMusic.exe");
                AMAttached = true;
                Console.WriteLine($"Attached to {AppleMusic.ProcessId}");
            }
            catch (Exception e)
            {
                Console.WriteLine("Apple Music not found", e.Message);
                AMAttached = false;
                client.ClearPresence();
            }
        }

        /* I realized we don't need Last.fm API to be here, bc we are making Apple Music RPC aren't we? so i decided to just use iTunes and go on.
         * might add later for the situation where iTunes api is down.

         static async Task<String> FetchImage(string ArtistAndAlbum, string lastFMAPIKey)
         {
             string encodedAlbumAndArtist = HttpUtility.UrlEncode(ArtistAndAlbum.Replace("—", "-"));
             try {
                 var lastReq = await hclient.GetAsync($"https://ws.audioscrobbler.com/2.0/?method=album.search&album={encodedAlbumAndArtist}&api_key={lastFMAPIKey}&format=json");
                 if (lastReq.IsSuccessStatusCode)
                 {
                     dynamic imageRes = JObject.Parse(await lastReq.Content.ReadAsStringAsync());
                     if ((imageRes["results"]["albummatches"]["album"]).Count != 0)
                     {
                         var image = imageRes["results"]["albummatches"]["album"][0]["image"][3]["#text"].ToString();
                         return image;
                     }
                     else
                     {
                         Console.WriteLine("Last.fm no image found");
                         return await FetchiTunes(encodedAlbumAndArtist);
                     }
                 }
                 else
                 {
                     Console.WriteLine("Last.fm request failed");
                     return await FetchiTunes(encodedAlbumAndArtist);
                 }
             }
             catch (Exception e)
             {
                 Console.WriteLine("last.fm Exception", e.Message);
                 return await FetchiTunes(encodedAlbumAndArtist);
             }
         }
        */

        static async Task<String[]> FetchiTunes(string songDetails)
        {
            try
            {
                //idk why but sometimes when you search as "Artist - Album Track" and if Album and Track named same it returns random song from album
                //ex: "Poppy - Negative Spaces negative spaces" Returns "Poppy - New Way Out" as a track link
                var iTunesReq = await hclient.GetAsync($"https://itunes.apple.com/search?term={songDetails}&limit=1&entity=song");
                if (iTunesReq.IsSuccessStatusCode)
                {
                    dynamic imageRes = JObject.Parse(await iTunesReq.Content.ReadAsStringAsync());
                    if (imageRes["resultCount"] != 0)
                    {
                        string[] res = {imageRes["results"][0]["artworkUrl100"].ToString(), imageRes["results"][0]["trackViewUrl"].ToString() };
                        return res;
                    }
                    else
                    {
                        Console.WriteLine("iTunes no image found");
                        return Array.Empty<string>();
                    }
                }
                else
                {
                    Console.WriteLine("iTunes request failed");
                    return Array.Empty<string>();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("iTunes Exception", e.Message);
                return Array.Empty<string>();
            }
        }

        static async void AMEvent()
        {
            using (var automation = new UIA3Automation())
            {
                var playingStatus = false;
                AutomationElement parent = null;
                AutomationElement[] listeningInfo = null;
                AutomationElement playButton = null;
                AutomationElement slider = null;

                while (!playingStatus)
                {
                    // I hate microsoft because of this
                    try
                    {
                        if (AppleMusic.HasExited)
                        {
                            AMAttached = false;
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        AMAttached = false;
                        break;
                    }

                    try
                    {
                        Window window = null;
                        var windows = AppleMusic.GetAllTopLevelWindows(automation);
                        if (windows.Length > 1)
                        {
                            for (var i = 0; i < windows.Length; i++)
                            {
                                if (windows[i].Name == "Apple Music") window = windows[i];
                            }
                        }
                        else if (windows.Length == 1)
                        {
                            window = windows[0];
                        }
                        parent = window.FindFirstChild(cf => cf.ByClassName("Microsoft.UI.Content.DesktopChildSiteBridge")).FindFirstChild().FindFirstChild().FindFirstChild(cf => cf.ByAutomationId("TransportBar"));
                        listeningInfo = parent.FindFirstChild(cf => cf.ByAutomationId("LCD")).FindAllChildren().Where(x => (x.AutomationId == "myScrollViewer")).ToArray();
                        playButton = parent.FindFirstChild(cf => cf.ByAutomationId("TransportControl_PlayPauseStop"));
                        slider = parent.FindFirstChild(cf => cf.ByAutomationId("LCD")).FindFirstChild(cf => cf.ByAutomationId("LCDScrubber"));
                        playingStatus = true;
                    }
                    catch (Exception e)
                    {
                        if (parent.FindFirstChild(cf => cf.ByAutomationId("TransportControl_PlayPauseStop"))?.Name != null && localizedPlay == null)
                        {
                            playButton = parent.FindFirstChild(cf => cf.ByAutomationId("TransportControl_PlayPauseStop"));
                            localizedPlay = playButton.Name;
                        }
                        Console.WriteLine(e.Message);
                    }
                    Thread.Sleep(50);
                }

                if (AMAttached)
                {
                    string previousSong = string.Empty;
                    string previousArtistAlbum = string.Empty;
                    bool resetStatus = false;

                    while (true)
                    {
                        if (AppleMusic.HasExited != true)
                        {
                            try
                            {
                                var currentSong = listeningInfo[0].Name;
                                var currentArtistAlbum = listeningInfo[1].Name;
                                var dashSplit = listeningInfo[1].Name.Split('-');
                                bool isSingle = dashSplit[dashSplit.Length - 1].Contains("Single");

                                if (currentSong != previousSong || currentArtistAlbum != previousArtistAlbum)
                                {
                                    // sometimes discord doesn't register rich presence idk why i tried everything...
                                    AMSongDataEvent.SongChange(new SongData(currentSong, (isSingle) ? string.Join("-", dashSplit.Take(dashSplit.Length - 1).ToArray()) : currentArtistAlbum));
                                    previousArtistAlbum = currentArtistAlbum;
                                    previousSong = currentSong;
                                }

                                if (localizedPlay == null && playButton?.Name == "Play")
                                {
                                    localizedPlay = playButton?.Name;
                                    client.ClearPresence();
                                    resetStatus = true;
                                }

                                if (playButton?.Name != null && localizedPlay != null && localizedPlay == playButton?.Name)
                                {
                                    client.ClearPresence();
                                    resetStatus = true;
                                }
                                else if (resetStatus == true && playButton?.Name != null && localizedPlay != null && localizedPlay != playButton?.Name)
                                {
                                    AMSongDataEvent.SongChange(new SongData(currentSong, (isSingle) ? string.Join("-", dashSplit.Take(dashSplit.Length - 1).ToArray()) : currentArtistAlbum));
                                    resetStatus = false;
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.Message);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Process Closed");
                            AMAttached = false;
                            client.ClearPresence();
                            while (!AMAttached)
                            {
                                AttachToAppleMusic();
                                Thread.Sleep(1000);
                            }
                            AMEvent();
                        }
                        Thread.Sleep(50);
                    }
                }
                else
                {
                    while (!AMAttached)
                    {
                        AttachToAppleMusic();
                        Thread.Sleep(1000);
                    }
                    AMEvent();
                }
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

        public class SongData : EventArgs
        {
            public string SongName { get; set; }
            public string ArtistandAlbumName { get; set; }

            public SongData(string songName, string artistandAlbumName)
            {
                SongName = songName;
                ArtistandAlbumName = artistandAlbumName;
            }
        }
    }
}