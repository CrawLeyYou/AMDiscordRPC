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

namespace AMDiscordRPC
{
    internal class Program
    {
        public static DiscordRpcClient client;
        public static HttpClient hclient = new HttpClient();
        public static FlaUI.Core.Application AppleMusic;
        public static bool AMAttached;
        static void Main(string[] args)
        {
            InitializeDiscordRPC();
            AttachToAppleMusic();
            AMSongDataEvent.SongChanged += async (sender, x) =>
             {
                 Console.WriteLine($"Song: {x.SongName}\nArtist and Album: {x.ArtistandAlbumName}");
                 var image = await FetchiTunes(HttpUtility.UrlEncode(x.ArtistandAlbumName.Replace("—", "-")));
                 client.SetPresence(new RichPresence()
                 {
                     Type = ActivityType.Listening,
                     Details = x.SongName,
                     State = x.ArtistandAlbumName.Split('—')[0],
                     Assets = new Assets()
                     {
                         LargeImageKey = image,
                         LargeImageText = x.ArtistandAlbumName.Split('—')[1],
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
                Console.WriteLine("Attached");
            }
            catch (Exception e)
            {
                Console.WriteLine("Apple Music not found", e.Message);
                AMAttached = false;
                ResetPresenceIdle();
            }
        }

        static void ResetPresenceIdle()
        {
            client.SetPresence(new RichPresence()
            {
                Type = ActivityType.Playing,
                Details = "Apple Music",
                State = "Idle",
                Assets = new Assets()
                {
                    LargeImageKey = "",
                    LargeImageText = "Apple Music",
                }
            });
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

        static async Task<String> FetchiTunes(string ArtistAndAlbum)
        {
            try
            {
                var iTunesReq = await hclient.GetAsync($"https://itunes.apple.com/search?term={ArtistAndAlbum}&limit=1&entity=song");
                if (iTunesReq.IsSuccessStatusCode)
                {
                    dynamic imageRes = JObject.Parse(await iTunesReq.Content.ReadAsStringAsync());
                    if (imageRes["resultCount"] != 0)
                    {
                        var image = imageRes["results"][0]["artworkUrl100"].ToString();
                        return image;
                    }
                    else
                    {
                        Console.WriteLine("iTunes no image found");
                        return string.Empty;
                    }
                }
                else
                {
                    Console.WriteLine("iTunes request failed");
                    return string.Empty;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("iTunes Exception", e.Message);
                return string.Empty;
            }
        }

        static async void AMEvent()
        {
            using (var automation = new UIA3Automation())
            {
                var playingStatus = false;
                AutomationElement parent = null;
                AutomationElement[] listeningInfo = null;

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
                        var window = AppleMusic.GetMainWindow(automation);
                        parent = window.FindFirstChild(cf => cf.ByClassName("Microsoft.UI.Content.DesktopChildSiteBridge")).FindFirstChild().FindFirstChild().FindFirstChild(cf => cf.ByAutomationId("TransportBar")).FindFirstChild(cf => cf.ByAutomationId("LCD"));
                        listeningInfo = parent.FindAllChildren().Where(x => (x.AutomationId == "myScrollViewer")).ToArray();
                        playingStatus = true;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }


                if (AMAttached)
                {
                    string previousSong = string.Empty;
                    string previousArtistAlbum = string.Empty;

                    while (true)
                    {
                        if (AppleMusic.HasExited != true)
                        {
                            try
                            {
                                var currentSong = listeningInfo[0].Name;
                                var currentArtistAlbum = listeningInfo[1]?.Name;
                                var dashSplit = listeningInfo[1].Name.Split('-');
                                bool isSingle = dashSplit[dashSplit.Length - 1].Contains("Single");

                                if (currentSong != previousSong || currentArtistAlbum != previousArtistAlbum)
                                {
                                    AMSongDataEvent.SongChange(new SongData(currentSong, (isSingle) ? string.Join("-", dashSplit.Take(dashSplit.Length - 1).ToArray()) : currentArtistAlbum));
                                    previousArtistAlbum = currentArtistAlbum;
                                    previousSong = currentSong;
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
                            ResetPresenceIdle();
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