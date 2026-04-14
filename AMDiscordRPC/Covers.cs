using Amazon.Runtime.Documents;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Markup;
using System.Xml.Linq;
using static AMDiscordRPC.Database;
using static AMDiscordRPC.Globals;
using static AMDiscordRPC.Playlist;

namespace AMDiscordRPC
{
    public class Covers
    {
        public static Task CoverThread;

        private static async Task<SQLSongResponse> AsyncFetchiTunes(AppleMusicScrapedData data)
        {
            try
            {
                //idk why but sometimes when you search as "Artist - Album Track" and if Album and Track named same it returns random song from album
                //ex: "Poppy - Negative Spaces negative spaces" Returns "Poppy - New Way Out" as a track link
                HttpResponseMessage iTunesReq = await hclient.GetAsync($"https://itunes.apple.com/search?term={data.GetSearchString()}&limit=1&entity=song&country={AMRegion}");
                if (iTunesReq.IsSuccessStatusCode)
                {
                    dynamic imageRes = JObject.Parse(await iTunesReq.Content.ReadAsStringAsync());
                    if (imageRes["resultCount"] != 0)
                    {
                        SQLSongResponse songData = new SQLSongResponse(
                            new SQLCoverData(0, imageRes["results"][0]["artworkUrl100"].ToString(), null, null, null),
                            new SQLAlbumData(0, data.AlbumName, imageRes["results"][0]["trackViewUrl"].ToString().Split(new string[] { "?i=" }, StringSplitOptions.None)[0], data.Type == SecondaryType.Single, 0, 0),
                            new SQLArtistData(0, data.ArtistName, imageRes["results"][0]["artistViewUrl"].ToString().Split(new string[] { "?uo=" }, StringSplitOptions.None)[0], await AsyncArtistProfileFetch(imageRes["results"][0]["artistViewUrl"].ToString().Split(new string[] { "?uo=" }, StringSplitOptions.None)[0])),
                            new SQLSongData(data.SongName, imageRes["results"][0]["trackViewUrl"].ToString().Split(new string[] { "&uo=" }, StringSplitOptions.None)[0], 0, 0)
                        );
                        InsertNew(songData);
                        CoverThread = null;
                        return songData;
                    }
                    else
                    {
                        log.Warn("iTunes no image found");
                        CoverThread = null;
                        return null;
                    }
                }
                else
                {
                    log.Warn("iTunes request failed");
                    CoverThread = null;
                    return null;
                }
            }
            catch (Exception e)
            {
                log.Error($"iTunes Exception {e.Message}");
                CoverThread = null;
                return null;
            }
        }

        /*
        private static async Task<WebSongResponse> AsyncFetchiTunes(string album, string searchStr)
        {
            try
            {
                //idk why but sometimes when you search as "Artist - Album Track" and if Album and Track named same it returns random song from album
                //ex: "Poppy - Negative Spaces negative spaces" Returns "Poppy - New Way Out" as a track link
                HttpResponseMessage iTunesReq = await hclient.GetAsync($"https://itunes.apple.com/search?term={searchStr}&limit=1&entity=song&country={AMRegion}");
                if (iTunesReq.IsSuccessStatusCode)
                {
                    dynamic imageRes = JObject.Parse(await iTunesReq.Content.ReadAsStringAsync());
                    if (imageRes["resultCount"] != 0)
                    {
                        WebSongResponse webRes = new WebSongResponse
                        (
                            imageRes["results"][0]["artworkUrl100"].ToString(),
                            imageRes["results"][0]["trackViewUrl"].ToString(),
                            imageRes["results"][0]["collectionName"].ToString(),
                            imageRes["results"][0]["artistViewUrl"].ToString()
                        );
                        InsertAlbum(new SQLCoverResponse(album, webRes.artworkURL, webRes.trackURL, null, null, null, webRes.artistURL));
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

    */
        public static async Task<string> AsyncArtistProfileFetch(string url)
        {
            try
            {
                HttpResponseMessage AMRequest = await hclient.GetAsync(url);
                if (AMRequest.IsSuccessStatusCode)
                {
                    string DOMasAString = await AMRequest.Content.ReadAsStringAsync();
                    IHtmlDocument document = parser.ParseDocument(DOMasAString);
                    if (document.QuerySelectorAll("div.artwork-component > picture > img")[1].GetAttribute("alt") == "") return null;
                    return document.QuerySelectorAll("div.artwork-component > picture > source")[1].GetAttribute("srcset").Split(',')[1].Split(' ')[0];
                }
                else
                {
                    log.Error($"Apple Music Artist request failed returned: {AMRequest.StatusCode}");
                }
            }
            catch (Exception e)
            {
                log.Error($"Apple Music Artist Request failed. {e}");
            }
            return null;
        }

        /*
        public static async Task<WebSongResponse> AsyncAMFetch(string album, string searchStr)
        {
            log.Debug($"https://music.apple.com/{AMRegion.ToLower()}/search?term={searchStr}");
            try
            {
                HttpResponseMessage AMRequest = await hclient.GetAsync($"https://music.apple.com/{AMRegion.ToLower()}/search?term={searchStr}");
                if (AMRequest.IsSuccessStatusCode)
                {
                    string DOMasAString = await AMRequest.Content.ReadAsStringAsync();
                    IHtmlDocument document = parser.ParseDocument(DOMasAString);

                    WebSongResponse webRes = new WebSongResponse(
                        document.DocumentElement.QuerySelectorAll("div.track-lockup__artwork-wrapper > div > picture > source")[1].GetAttribute("srcset").Split(',')[1].Split(' ')[0],
                        document.DocumentElement.QuerySelectorAll("div.track-lockup__clamp-wrapper > a")[0].GetAttribute("href"),
                        null,
                        document.DocumentElement.QuerySelectorAll("div.track-lockup__clamp-wrapper > span > a")[0].GetAttribute("href")
                    );
                    CoverThread = null;
                    InsertAlbum(new Database.SQLCoverResponse(album, webRes.artworkURL, webRes.trackURL, null, null, null, webRes.artistURL));
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
         */

        public static async Task<SQLSongResponse> AsyncAMFetch(AppleMusicScrapedData data)
        {
            log.Debug($"https://music.apple.com/{AMRegion.ToLower()}/search?term={data.GetSearchString()}");
            try
            {
                HttpResponseMessage AMRequest = await hclient.GetAsync($"https://music.apple.com/{AMRegion.ToLower()}/search?term={data.GetSearchString()}");
                if (AMRequest.IsSuccessStatusCode)
                {
                    string DOMasAString = await AMRequest.Content.ReadAsStringAsync();
                    IHtmlDocument document = parser.ParseDocument(DOMasAString);
                   
                    // These can be simplified later but it works rn. ALSO ALSO SOMEHOW 2 CHECKS ISNT ENOUGH FOR SOME CASES BUT IM NOT GONNA COVER ALL CASES ATLEAST NOW
                    SQLSongResponse songData = new SQLSongResponse(
                            new SQLCoverData(0, document.DocumentElement.QuerySelectorAll("div.track-lockup__artwork-wrapper > div > picture > source")[1].GetAttribute("srcset").Split(',')[1].Split(' ')[0], null, null, null),
                            new SQLAlbumData(0, data.AlbumName, (document.DocumentElement.QuerySelectorAll("a.product-lockup__link")[0].ParentElement.ParentElement.ParentElement.QuerySelectorAll("div.product-lockup__content > div > div > div > span > a")[0].TextContent == data.AlbumName) ? document.DocumentElement.QuerySelectorAll("a.product-lockup__link")[0].GetAttribute("href") : (document.DocumentElement.QuerySelectorAll("a.product-lockup__link")[1].ParentElement.ParentElement.ParentElement.QuerySelectorAll("div.product-lockup__content > div > div > div > span > a")[0].TextContent == data.AlbumName) ? document.DocumentElement.QuerySelectorAll("a.product-lockup__link")[1].GetAttribute("href") : document.DocumentElement.QuerySelectorAll("a.product-lockup__link")[0].GetAttribute("href"), data.Type == SecondaryType.Single, 0, 0),
                            new SQLArtistData(0, data.ArtistName, document.DocumentElement.QuerySelectorAll("div.track-lockup__clamp-wrapper > span > a")[0].GetAttribute("href"), (document.DocumentElement.QuerySelectorAll("div.track-lockup__clamp-wrapper > span > a")[0].GetAttribute("href") == document.DocumentElement.QuerySelectorAll("div.artwork")[0].ParentElement.ParentElement.GetAttribute("href")) ? document.DocumentElement.QuerySelectorAll("div.artwork > div > picture > source")[0].GetAttribute("srcset").Split(',')[1].Split(' ')[0].Replace("webp", "jpg") : await AsyncArtistProfileFetch(document.DocumentElement.QuerySelectorAll("div.track-lockup__clamp-wrapper > span > a")[0].GetAttribute("href"))),
                            new SQLSongData(data.SongName, (document.DocumentElement.QuerySelectorAll("div.track-lockup__clamp-wrapper > a")[0].TextContent == data.SongName) ? document.DocumentElement.QuerySelectorAll("div.track-lockup__clamp-wrapper > a")[0].GetAttribute("href") : (document.DocumentElement.QuerySelectorAll("div.track-lockup__clamp-wrapper > a")[1].TextContent == data.SongName) ? document.DocumentElement.QuerySelectorAll("div.track-lockup__clamp-wrapper > a")[1].GetAttribute("href") : document.DocumentElement.QuerySelectorAll("div.track-lockup__clamp-wrapper > a")[0].GetAttribute("href"), 0, 0)
                    );

                    InsertNew(songData);
                    CoverThread = null;
                    return songData;
                }
                else
                {
                    log.Error($"Apple Music request failed returned: {AMRequest.StatusCode}");
                    return await AsyncFetchiTunes(data);
                }
            }
            catch (Exception e)
            {
                log.Error($"Apple Music Request failed. {e}");
                return await AsyncFetchiTunes(data);
            }
        }

        public static async Task CheckAnimatedCover(string album, string url, CancellationToken ct)
        {
            try
            {
                var appleMusicDom = await hclient.GetAsync(url);
                log.Debug($"Animated Cover Request: {url}");
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

        /*
         * old method will deleted
        public static async Task<WebSongResponse> GetCover(string album, string searchStr)
        {
            try
            {
                log.Debug($"https://music.apple.com/{AMRegion.ToLower()}/search?term={searchStr}");
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
        */

        public static async Task<SQLRPCResponse> GetCover(AppleMusicScrapedData data)
        {
            try
            {
                SQLRPCResponse cover = GetSongFromDB(data.SongName, data.AlbumName, data.ArtistName);
                if (cover != null)
                {
                    CoverThread = null;
                    return cover;
                }
                else
                {
                    SQLSongResponse songData = await AsyncAMFetch(data);
                    if (songData == null) return new SQLRPCResponse();
                    return new SQLRPCResponse
                    {
                        coverURL = songData.cover.staticCoverURL,
                        artistRedirURL = songData.artist.artistRedirURL,
                        artistProfileSource = songData.artist.artistProfileSource,
                        albumURL = songData.album.albumURL,
                        songURL = songData.song.songURL
                    };
                }
            }
            catch (Exception ex)
            {
                SQLSongResponse songData = await AsyncAMFetch(data);
                if (songData == null) return new SQLRPCResponse();
                return new SQLRPCResponse
                {
                    coverURL = songData.cover.staticCoverURL,
                    artistRedirURL = songData.artist.artistRedirURL,
                    artistProfileSource = songData.artist.artistProfileSource,
                    albumURL = songData.album.albumURL,
                    songURL = songData.song.songURL
                };
            }
        }

        /* I realized we don't need Last.fm API to be here, bc we are making Apple Music RPC aren't we? so i decided to just use iTunes and go on.
        * might add later for the situation where iTunes api is down.
        * probably not gonna add it since prefered source is now Apple Music and we have iTunes as a backup.

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
