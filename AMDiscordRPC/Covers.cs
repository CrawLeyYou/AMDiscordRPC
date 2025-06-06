﻿using AngleSharp.Html.Dom;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;
using static AMDiscordRPC.Globals;
using static AMDiscordRPC.Playlist;

namespace AMDiscordRPC
{
    public class Covers
    {
        public static Task CoverThread;

        public static async Task<String[]> AsyncFetchiTunes(string album, string searchStr)
        {
            try
            {
                //idk why but sometimes when you search as "Artist - Album Track" and if Album and Track named same it returns random song from album
                //ex: "Poppy - Negative Spaces negative spaces" Returns "Poppy - New Way Out" as a track link
                var iTunesReq = await hclient.GetAsync($"https://itunes.apple.com/search?term={searchStr}&limit=1&entity=song");
                if (iTunesReq.IsSuccessStatusCode)
                {
                    dynamic imageRes = JObject.Parse(await iTunesReq.Content.ReadAsStringAsync());
                    if (imageRes["resultCount"] != 0)
                    {
                        string[] res = { imageRes["results"][0]["artworkUrl100"].ToString(), imageRes["results"][0]["trackViewUrl"].ToString(), imageRes["results"][0]["collectionName"].ToString() };
                        Database.UpdateAlbum(new Database.SQLCoverResponse(album, res[0], res[1], null, null, null));
                        CoverThread = null;
                        return res;
                    }
                    else
                    {
                        log.Warn("iTunes no image found");
                        CoverThread = null;
                        return Array.Empty<string>();
                    }
                }
                else
                {
                    log.Warn("iTunes request failed");
                    CoverThread = null;
                    return Array.Empty<string>();
                }
            }
            catch (Exception e)
            {
                log.Error($"iTunes Exception {e.Message}");
                CoverThread = null;
                return Array.Empty<string>();
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
                Database.UpdateAlbum(new Database.SQLCoverResponse(album, null, null, false, null, null));
            }
        }

        public static async Task<string[]> GetCover(string album, string searchStr)
        {
            try
            {
                Database.SQLCoverResponse cover = Database.GetAlbumDataFromSQL(album);
                if (cover != null)
                {
                    string[] res = { (cover.animated == true && cover.animatedURL != null) ? cover.animatedURL : (cover.source != null) ? cover.source : throw new Exception("Source not found."), cover.redirURL, album };
                    return res;
                }
                else return await AsyncFetchiTunes(album, searchStr);
            }
            catch (Exception ex)
            {
                return await AsyncFetchiTunes(album, searchStr);
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
