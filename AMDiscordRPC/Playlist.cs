using AngleSharp.Text;
using FlaUI.Core.Tools;
using M3U8Parser;
using M3U8Parser.Tags.MultivariantPlaylist;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static AMDiscordRPC.Discord;
using static AMDiscordRPC.Globals;
using static AMDiscordRPC.S3;

namespace AMDiscordRPC
{
    internal class Playlist
    {
        public static async Task<string> ConvertM3U8(string playlistUrl)
        {
            //Database isAnimated = true here
            //Database streamURL = playlistURL
            // ^I thought storing Master Playlist would be better for in case of bucket changes and Apple's codec changes on lowest quality.
            StreamInf playlist = await FetchResolution(playlistUrl);
            if (playlist != null)
            {
                string[] splitUrl = playlist.Uri.Split('/');
                string fileName = await FetchFileName(playlist.Uri);
                string newURL = string.Join("/", splitUrl.Take(splitUrl.Length - 1)) + $"/{fileName}";
                Directory.CreateDirectory($@"{Application.StartupPath}\temp\");
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFile(newURL, $@"{Application.StartupPath}\temp\{fileName}");
                    }
                    log.Debug("Downloaded cover");
                    string gifPath = await ConvertToGIF(fileName, playlist.FrameRate);
                    log.Debug($"Converted to GIF. Path: {gifPath}");
                    string servedPath = await PutGIF(gifPath, fileName.Replace(".mp4", ".gif"));
                    log.Debug("Put S3 Bucket");
                    //Database animatedURL = servedPath
                    SetCover(servedPath);
                    log.Debug("Set Animated Cover");
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

        public static async Task<string> ConvertToGIF(string fileName, decimal? fps)
        {
            Process proc = new Process();
            proc.StartInfo.FileName = ffmpegPath;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.Arguments = $@"-y -i ""{Application.StartupPath}\temp\{fileName}"" -vf ""fps={Decimal.ToInt32((decimal)fps)},scale=300:-1:flags=lanczos"" ""{Application.StartupPath}\temp\{fileName.Replace(".mp4", ".gif")}""";
            proc.Start();
            proc.WaitForExit();
            return $@"{Application.StartupPath}\temp\{fileName.Replace(".mp4", ".gif")}";
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
