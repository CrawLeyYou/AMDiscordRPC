using M3U8Parser;
using M3U8Parser.Tags.MultivariantPlaylist;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static AMDiscordRPC.Discord;
using static AMDiscordRPC.Globals;

namespace AMDiscordRPC
{
    internal class Playlist
    {
        public static async Task<string> ConvertM3U8(string playlistUrl)
        {
            StreamInf playlist = await FetchResolution(playlistUrl);
            if (playlist != null)
            {
                string[] splitUrl = playlist.Uri.Split('/');
                string fileName = await FetchFileName(playlist.Uri);
                string newUrl = string.Join("/", splitUrl.Take(splitUrl.Length - 1)) + $"/{fileName}";

                Directory.CreateDirectory($@"{Application.StartupPath}\temp\");
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFile(newUrl, $@"{Application.StartupPath}\temp\{fileName}");
                    }
                    log.Debug("Downloaded cover");
                    //SetCover("https://raw.githubusercontent.com/CrawLeyYou/raw/refs/heads/main/output.gif");
                }
                catch (Exception e)
                {
                    log.Error($"Download failed: {e}");
                    return null;
                }
            }
            return null;
        }

        public static async Task<string> FetchFileName(string playlistUrl)
        {
            try
            {
                HttpResponseMessage resp = await hclient.GetAsync(playlistUrl);

                if (resp.IsSuccessStatusCode)
                {
                    MediaPlaylist mediaPlaylist = MediaPlaylist.LoadFromText(await resp.Content.ReadAsStringAsync());
                    return mediaPlaylist.Map.Uri;
                }
                else
                {
                    log.Error($"Media playlist request failed");
                    return null;
                }
            }
            catch (Exception e)
            {
                log.Error($"An error occured while fetching the media playlist: {e}");
                return null;
            }
        }

        public static async Task<StreamInf> FetchResolution(string playlistUrl)
        {
            try
            {
                HttpResponseMessage resp = await hclient.GetAsync(playlistUrl);
                if (resp.IsSuccessStatusCode)
                {
                    MasterPlaylist playlist = MasterPlaylist.LoadFromText(await resp.Content.ReadAsStringAsync());

                    List<StreamInf> sortedList = playlist.Streams.Where(s => s.Codecs == "avc1.64001f").ToList();
                    sortedList.Sort((StreamInf x, StreamInf y) => x.Bandwidth.CompareTo(y.Bandwidth));
                    return sortedList[0];
                }
                else
                {
                    log.Error($"Master playlist request failed");
                    return null;
                }
            }
            catch (Exception e)
            {
                log.Error($"An error occured while fetching the master playlist: {e}");
                return null;
            }
        }
    }
}
