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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static AMDiscordRPC.Discord;
using static AMDiscordRPC.Globals;
using static AMDiscordRPC.S3;

namespace AMDiscordRPC
{
    internal class Playlist
    {
        public static async Task ConvertM3U8(string album, string playlistUrl, CancellationToken ct)
        {
            // ^I thought storing Master Playlist would be better for in case of bucket changes and Apple's codec changes on lowest quality.
            Database.UpdateAlbum(new Database.SQLCoverResponse(album, null, null, true, playlistUrl, null));
            StreamInf playlist = await FetchResolution(playlistUrl);
            if (!ct.IsCancellationRequested && playlist != null)
            {
                string[] splitUrl = playlist.Uri.Split('/');
                if (ct.IsCancellationRequested) throw new Exception("Cancelled");
                string fileName = await FetchFileName(playlist.Uri);
                string newURL = string.Join("/", splitUrl.Take(splitUrl.Length - 1)) + $"/{fileName}";
                string servedPath = null;
                string gifPath = null;
                Directory.CreateDirectory($@"{Application.StartupPath}\temp\");
                try
                {
                    if (ct.IsCancellationRequested) throw new Exception("Cancelled");
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFile(newURL, $@"{Application.StartupPath}\temp\{fileName}");
                    }
                    log.Debug("Downloaded cover");
                    if (ct.IsCancellationRequested) throw new Exception("Cancelled");
                    if (ffmpegPath != null) gifPath = await ConvertToGIF(fileName, playlist.FrameRate);
                    else throw new Exception("FFmpeg not found");
                    log.Debug($"Converted to GIF. Path: {gifPath}");
                    if (ct.IsCancellationRequested) throw new Exception("Cancelled");
                    if (S3_Credentials != null && S3_Credentials.GetNullKeys().Count == 0) servedPath = await PutGIF(gifPath, fileName.Replace(".mp4", ".gif"));
                    else throw new Exception("S3 is not properly configured.");
                    log.Debug("Put S3 Bucket");
                    if (ct.IsCancellationRequested) throw new Exception("Cancelled");
                    Database.UpdateAlbum(new Database.SQLCoverResponse(album, null, null, null, null, servedPath));
                    if (ct.IsCancellationRequested) throw new Exception("Cancelled");
                    SetCover(servedPath);
                    log.Debug("Set Animated Cover");
                }
                catch (Exception e)
                {
                    log.Error($"Download failed: {e}");
                }
            }
            Discord.animatedCoverCts = null;
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
