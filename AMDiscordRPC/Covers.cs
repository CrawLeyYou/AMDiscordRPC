using AngleSharp.Html.Dom;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Dom;
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

        public static async Task<SQLSongResponse> AsyncAMFetch(AppleMusicScrapedData data)
        {
            log.Debug($"https://music.apple.com/{AMRegion.ToLower()}/search?term={data.GetSearchString()}");
            try
            {
                HttpResponseMessage AMRequest = await hclient.GetAsync($"https://music.apple.com/{AMRegion.ToLower()}/search?term={data.GetSearchString()}");
                if (AMRequest.IsSuccessStatusCode)
                {
                    string DOMasAString = await AMRequest.Content.ReadAsStringAsync();
                    IElement document = parser.ParseDocument(DOMasAString).DocumentElement;
                    string[] artistData = document.GetArtist(data);
                    SQLSongResponse songData = new SQLSongResponse(
                            new SQLCoverData(0, document.GetCover(data), null, null, null),
                            new SQLAlbumData(0, data.AlbumName, document.GetAlbum(data), data.Type == SecondaryType.Single, 0, 0),
                            new SQLArtistData(0, data.ArtistName, artistData[0], artistData[1]),
                            new SQLSongData(data.SongName, document.GetSong(data), 0, 0)
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

        public static async Task CheckAnimatedCover(string albumUrl, CancellationToken ct)
        {
            try
            {
                var appleMusicDom = await hclient.GetAsync(albumUrl);
                log.Debug($"Animated Cover Request: {albumUrl}");
                if (appleMusicDom.IsSuccessStatusCode)
                {
                    string DOMasAString = await appleMusicDom.Content.ReadAsStringAsync();
                    IHtmlDocument document = parser.ParseDocument(DOMasAString);
                    ConvertM3U8(albumUrl, document.DocumentElement.QuerySelector("div.video-artwork__container").InnerHtml.Split(new string[] { "src=\"" }, StringSplitOptions.None)[1].Split('"')[0], ct);
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
                UpdateAlbumCover(albumUrl, new SQLCoverData(0, null, false, null, null));
            }
        }

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
