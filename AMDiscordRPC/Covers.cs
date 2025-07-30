using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Newtonsoft.Json.Linq;
using System;
using System.Data.SQLite;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static AMDiscordRPC.Database;
using static AMDiscordRPC.Globals;
using static AMDiscordRPC.Playlist;

namespace AMDiscordRPC
{
    public class Covers
    {
        public static Task CoverThread;

        private static async Task<WebSongResponse> AsyncFetchiTunes(string album, string searchStr)
        {
            try
            {
                //idk why but sometimes when you search as "Artist - Album Track" and if Album and Track named same it returns random song from album
                //ex: "Poppy - Negative Spaces negative spaces" Returns "Poppy - New Way Out" as a track link
                HttpResponseMessage iTunesReq = await hclient.GetAsync($"https://itunes.apple.com/search?term={searchStr}&limit=1&entity=song");
                if (iTunesReq.IsSuccessStatusCode)
                {
                    dynamic imageRes = JObject.Parse(await iTunesReq.Content.ReadAsStringAsync());
                    if (imageRes["resultCount"] != 0)
                    {
                        WebSongResponse webRes = new WebSongResponse
                        (
                            imageRes["results"][0]["artworkUrl100"].ToString(),
                            imageRes["results"][0]["trackViewUrl"].ToString(),
                            imageRes["results"][0]["collectionName"].ToString()
                        );
                        Database.UpdateAlbum(new Database.SQLCoverResponse(album, webRes.artworkURL, webRes.trackURL));
                        CoverThread = null;
                        return webRes;
                    }
                    else
                    {
                        log.Warn("iTunes no image found");
                        CoverThread = null;
                        return new WebSongResponse();
                    }
                }
                else
                {
                    log.Warn("iTunes request failed");
                    CoverThread = null;
                    return new WebSongResponse();
                }
            }
            catch (Exception e)
            {
                log.Error($"iTunes Exception {e.Message}");
                CoverThread = null;
                return new WebSongResponse();
            }
        }

        public static async Task<WebSongResponse> AsyncAMFetch(string album, string searchStr) 
        {
            try
            {
                HttpResponseMessage AMRequest = await hclient.GetAsync($"https://music.apple.com/tr/search?term={searchStr}");
                if (AMRequest.IsSuccessStatusCode)
                {
                    string DOMasAString = await AMRequest.Content.ReadAsStringAsync();
                    IHtmlDocument document = parser.ParseDocument(DOMasAString);
                    WebSongResponse webRes = new WebSongResponse(
                        document.DocumentElement.QuerySelectorAll("div.top-search-lockup__artwork > div > picture > source")[1].GetAttribute("srcset").Split(' ')[0],
                        document.DocumentElement.QuerySelector("div.top-search-lockup__action > a").GetAttribute("href")
                    );
                    CoverThread = null;
                    Database.UpdateAlbum(new Database.SQLCoverResponse(album, webRes.artworkURL, webRes.trackURL));
                    return webRes;
                }
                else 
                {
                    log.Error($"Apple Music request failed returned: {AMRequest.StatusCode}");
                    return await AsyncFetchiTunes(album, searchStr);
                }
            }
            catch (Exception e) 
            {
                log.Error($"Apple Music Request failed. {e}");
                return await AsyncFetchiTunes(album, searchStr);
            }
        }

        public static async Task CheckAnimatedCover(string album, string url, CancellationToken ct)
        {
            try
            {
                var appleMusicDom = await hclient.GetAsync(url);
                if (appleMusicDom.IsSuccessStatusCode)
                {
                    string DOMasAString = await appleMusicDom.Content.ReadAsStringAsync();
                    IHtmlDocument document = parser.ParseDocument(DOMasAString);
                    ConvertM3U8(album, document.DocumentElement.QuerySelector("div.video-artwork__container").InnerHtml.Split(new string[] { "src=\"" }, StringSplitOptions.None)[1].Split('"')[0], ct);
                }
                else
                {
                    log.Error($"Apple Music request failed");
                    Discord.animatedCoverCts = null;
                }
            }
            catch (Exception e)
            {
                log.Error($"Apple Music animatedCover exception: {e.Message}");
                Discord.animatedCoverCts = null;
                Database.UpdateAlbum(new Database.SQLCoverResponse(album, null, null, false));
            }
        }

        public static async Task<WebSongResponse> GetCover(string album, string searchStr)
        {
            try
            {
                log.Debug($"https://music.apple.com/us/search?term={searchStr}");
                SQLCoverResponse cover = GetAlbumDataFromSQL(album);
                if (cover != null)
                {
                    WebSongResponse res = new WebSongResponse
                    (
                        (cover.animated == true && cover.animatedURL != null) ? cover.animatedURL : (cover.source != null) ? cover.source : throw new Exception("Source not found."),
                        cover.redirURL,
                        album
                    );
                    CoverThread = null;
                    return res;
                }
                else
                {
                    return await AsyncAMFetch(album, searchStr);
                }
            }
            catch (Exception ex)
            {
                return await AsyncAMFetch(album, searchStr);
            }
        }

        

        /* I realized we don't need Last.fm API to be here, bc we are making Apple Music RPC aren't we? so i decided to just use iTunes and go on.
        * might add later for the situation where iTunes api is down.

        public static async Task<String> FetchImage(string ArtistAndAlbum, string lastFMAPIKey)
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
                        log.Info("Last.fm no image found");
                        return await FetchiTunes(encodedAlbumAndArtist);
                    }
                }
                else
                {
                    log.Info("Last.fm request failed");
                    return await FetchiTunes(encodedAlbumAndArtist);
                }
            }
            catch (Exception e)
            {
                log.Info("last.fm Exception", e.Message);
                return await FetchiTunes(encodedAlbumAndArtist);
            }
        }
       */
    }
}
