﻿using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;
using static AMDiscordRPC.Globals;

namespace AMDiscordRPC
{
    public class Covers
    {
        public static Thread CoverThread;

        public static async Task<String[]> AsyncFetchiTunes(string songDetails)
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
                        string[] res = { imageRes["results"][0]["artworkUrl100"].ToString(), imageRes["results"][0]["trackViewUrl"].ToString(), imageRes["results"][0]["collectionName"].ToString() };
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

        /* Discord doesn't support M3U8 at this time, but when M3U8 support comes we gonna have AnimatedCovers
         * 
        public static async Task<String> CheckAnimatedCover(string url, string songDetails)
        {
            try
            {
                var appleMusicDom = await hclient.GetAsync(url);
                if (appleMusicDom.IsSuccessStatusCode)
                {
                    string DOMasAString = await appleMusicDom.Content.ReadAsStringAsync();
                    IHtmlDocument document = parser.ParseDocument(DOMasAString);
                    return document.DocumentElement.QuerySelector("div.video-artwork__container").InnerHtml.Split(new string[] { "src=\"" }, StringSplitOptions.None)[1].Split('"')[0];
                }
                else
                {
                    log.Error($"Apple Music request failed");
                    return null;
                }
            }
            catch (Exception e)
            {
                log.Error($"Apple Music fetch exception: {e.Message}");
                return null;
            }
        }
        */

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
