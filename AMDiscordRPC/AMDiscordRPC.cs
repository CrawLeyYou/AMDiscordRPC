using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using static AMDiscordRPC.AppleMusic;
using static AMDiscordRPC.Covers;
using static AMDiscordRPC.Database;
using static AMDiscordRPC.Discord;
using static AMDiscordRPC.Globals;
using static AMDiscordRPC.S3;
using static AMDiscordRPC.UI;

namespace AMDiscordRPC
{
    internal class AMDiscordRPC
    {
        private static string oldAlbumnArtist;
        static void Main(string[] args)
        {
            InitRegion();
            CreateUI();
            ConfigureLogger();
            InitializeDiscordRPC();
            AttachToAppleMusic();
            AMSongDataEvent.SongChanged += async (sender, x) =>
             {
                 log.Info($"Song: {x.SongName} \\ Artist and Album: {x.ArtistandAlbumName}");
                 AMDiscordRPCTray.ChangeSongState($"{x.ArtistandAlbumName.Split('—')[0]} - {x.SongName}");
                 if (x.ArtistandAlbumName == oldAlbumnArtist && oldData.Assets.LargeImageKey != null)
                 {
                     SetPresence(x);
                 }
                 else
                 {
                     if (httpRes.Equals(new WebSongResponse()) || CoverThread != null)
                     {
                         httpRes = await GetCover(x.ArtistandAlbumName.Split('—')[1], Uri.EscapeDataString(x.ArtistandAlbumName + $" {x.SongName}"));
                         log.Debug($"Set Cover: {((httpRes.artworkURL != null) ? httpRes.artworkURL : null)}");
                     }
                     SetPresence(x, httpRes);
                     oldAlbumnArtist = x.ArtistandAlbumName;
                 }
             };
            CheckDatabaseIntegrity();
            InitDBCreds();
            CheckFFmpeg();
            InitS3();
            AMEvent();
        }

        static void AMEvent()
        {
            using (var automation = new UIA3Automation())
            {
                var playingStatus = false;
                AutomationElement parent = null;
                AutomationElement[] listeningInfo = null;
                AutomationElement LCDInf = null;
                AutomationElement audioBadge = null;
                AutomationElement playButton = null;
                AutomationElement slider = null;

                while (!playingStatus)
                {
                    // I hate microsoft because of this
                    try
                    {
                        if (AppleMusicProc.HasExited)
                        {
                            AMAttached = false;
                            break;
                        }
                    }
                    catch (Exception)
                    {
                        AMAttached = false;
                        break;
                    }

                    try
                    {
                        Window window = null;
                        var windows = AppleMusicProc.GetAllTopLevelWindows(automation);
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
                        playButton = parent.FindFirstChild(cf => cf.ByAutomationId("TransportControl_PlayPauseStop"));
                        LCDInf = parent.FindFirstChild(cf => cf.ByAutomationId("LCD"));
                        listeningInfo = LCDInf.FindAllChildren().Where(x => (x.ControlType == ControlType.Pane)).ToArray();
                        slider = LCDInf.FindFirstChild(cf => cf.ByAutomationId("LCDScrubber"));
                        if (slider == null) throw new FieldAccessException("Slider not found");
                        playingStatus = true;
                    }
                    catch (Exception e)
                    {
                        try
                        {
                            if (localizedPlay == null && playButton?.Name != null && playButton?.IsEnabled == false)
                            {
                                localizedPlay = playButton.Name;
                                log.Debug($"Localized play found: {playButton.Name}");
                            }
                        }
                        catch (Exception eX)
                        {
                            log.Error(eX.Message);
                        }
                        log.Debug(e.Message);
                    }
                    Thread.Sleep(50);
                }

                if (AMAttached)
                {
                    string previousSong = string.Empty;
                    string previousArtistAlbum = string.Empty;
                    string lastFetchedArtistAlbum = string.Empty;
                    AudioFormat format = AudioFormat.AAC;
                    bool resetStatus = false;
                    double oldValue = 0;

                    while (true)
                    {
                        if (AppleMusicProc.HasExited != true)
                        {
                            try
                            {
                                var currentSong = listeningInfo[0].Name;
                                var currentArtistAlbum = (listeningInfo[1].Properties.Name.IsSupported == true) ? listeningInfo[1].Name : lastFetchedArtistAlbum;
                                var dashSplit = currentArtistAlbum.Split('-');
                                var subractThis = TimeSpan.FromSeconds(slider.AsSlider().Value + 1);
                                if (oldValue == 0) oldValue = slider.AsSlider().Value;
                                DateTime currentTime = DateTime.UtcNow;
                                DateTime startTime = currentTime.Subtract(subractThis);
                                DateTime endTime = currentTime.AddSeconds(slider.AsSlider().Maximum).Subtract(subractThis);
                                DateTime oldEndTime = DateTime.MinValue;
                                DateTime oldStartTime = DateTime.MinValue;
                                bool isSingle = dashSplit[dashSplit.Length - 1].Contains("Single");
                                audioBadge = LCDInf.FindFirstChild(cf => cf.ByAutomationId("AudioBadgeButton"));

                                if (!playButton.IsEnabled && playButton?.Name != null && localizedPlay == null)
                                {
                                    log.Debug($"Localized play found: {playButton.Name}");
                                    localizedPlay = playButton.Name;
                                }

                                if (oldValue <= slider.AsSlider().Value && (slider.AsSlider().Value - oldValue) <= 1 && !resetStatus)
                                {
                                    if ((slider.AsSlider().Value - oldValue) == 1 && localizedPlay == null && localizedStop == null)
                                    {
                                        localizedStop = playButton.Name;
                                        log.Debug($"Localized stop found: {localizedStop}");
                                    }
                                    oldValue = slider.AsSlider().Value;
                                }
                                else if (resetStatus == false && slider.AsSlider().Maximum != 0 && oldValue != 0 && currentSong == previousSong && currentArtistAlbum == previousArtistAlbum && startTime != endTime)
                                {
                                    AMDiscordRPCTray.ChangeSongState($"{((isSingle) ? string.Join("-", dashSplit.Take(dashSplit.Length - 1).ToArray()) : string.Join("—", currentArtistAlbum.Split('—').Take(2).ToArray())).Split('—')[0]} - {currentSong}");
                                    ChangeTimestamps(startTime, endTime);
                                    oldValue = slider.AsSlider().Value;
                                }

                                if (currentArtistAlbum != lastFetchedArtistAlbum)
                                {
                                    if (CoverThread != null)
                                    {
                                        CoverThread.Dispose();
                                        log.Debug("Previous thread disposed");
                                    }
                                    else log.Debug("Continue");
                                    string idontknowwhatshouldinamethisbutitsaboutalbum = (isSingle) ? string.Join("-", dashSplit.Take(dashSplit.Length - 1).ToArray()) : string.Join("—", currentArtistAlbum.Split('—').Take(2).ToArray());
                                    CheckAndInsertAlbum(idontknowwhatshouldinamethisbutitsaboutalbum.Split('—')[1]);
                                    Task t = new Task(async () =>
                                    {
                                        httpRes = await GetCover(idontknowwhatshouldinamethisbutitsaboutalbum.Split('—')[1], Uri.EscapeDataString((isSingle) ? string.Join("-", dashSplit.Take(dashSplit.Length - 1).ToArray()) : string.Join("—", currentArtistAlbum.Split('—').Take(2).ToArray()) + $" {currentSong}"));
                                        log.Debug($"Set Cover: {((httpRes.artworkURL != null) ? httpRes.artworkURL : null)}");
                                    });
                                    CoverThread = t;
                                    t.Start();
                                    lastFetchedArtistAlbum = currentArtistAlbum;
                                }

                                if (slider.AsSlider().Maximum != 0 && slider.AsSlider().Value != 0 && endTime != startTime && (currentSong != previousSong || currentArtistAlbum != previousArtistAlbum) && oldEndTime != endTime && oldStartTime != startTime)
                                {
                                    // sometimes discord doesn't register rich presence idk why i tried everything...
                                    previousArtistAlbum = currentArtistAlbum;
                                    previousSong = currentSong;
                                    if (audioBadge != null)
                                    {
                                        switch (audioBadge?.Name)
                                        {
                                            case "Dolby Atmos":
                                                format = AudioFormat.Dolby_Atmos;
                                                break;
                                            case "Dolby Audio":
                                                format = AudioFormat.Dolby_Audio;
                                                break;
                                            default:
                                                format = AudioFormat.Lossless;
                                                break;
                                        }
                                    }
                                    else format = AudioFormat.AAC;
                                    oldValue = 0;
                                    startTime = currentTime.Subtract(subractThis);
                                    endTime = currentTime.AddSeconds(slider.AsSlider().Maximum).Subtract(subractThis);
                                    oldStartTime = startTime;
                                    oldEndTime = endTime;
                                    AMSongDataEvent.ChangeSong(new SongData(currentSong, (isSingle) ? string.Join("-", dashSplit.Take(dashSplit.Length - 1).ToArray()) : string.Join("—", currentArtistAlbum.Split('—').Take(2).ToArray()), currentArtistAlbum.Split('—').Length <= 1, startTime, endTime, format));
                                }

                                if (playButton?.Name != null && (localizedPlay != null && localizedPlay == playButton?.Name || localizedStop != null && localizedStop != playButton?.Name))
                                {
                                    localizedPlay = playButton.Name;
                                    AMDiscordRPCTray.ChangeSongState("AMDiscordRPC");
                                    client.ClearPresence();
                                    resetStatus = true;
                                }
                                else if (resetStatus == true && playButton?.Name != null && localizedPlay != null && localizedPlay != playButton?.Name && slider.AsSlider().Maximum != 0)
                                {
                                    AMDiscordRPCTray.ChangeSongState($"{((isSingle) ? string.Join("-", dashSplit.Take(dashSplit.Length - 1).ToArray()) : string.Join("—", currentArtistAlbum.Split('—').Take(2).ToArray())).Split('—')[0]} - {currentSong}");
                                    ChangeTimestamps(startTime, endTime);
                                    resetStatus = false;
                                }
                            }
                            catch (Exception e)
                            {
                                log.Error(e.Message);
                                AMAttached = false;
                                break;
                            }
                        }
                        else
                        {
                            log.Info("Process Closed");
                            AMAttached = false;
                            client.ClearPresence();
                            while (!AMAttached)
                            {
                                AttachToAppleMusic();
                                Thread.Sleep(1000);
                            }
                            AMEvent();
                        }
                        Thread.Sleep(20);
                    }
                    if (!AMAttached & AppleMusicProc.HasExited != true)
                    {
                        log.Info("Something happened which needs to reattach");
                        client.ClearPresence();
                        while (!AMAttached)
                        {
                            AttachToAppleMusic();
                            Thread.Sleep(1000);
                        }
                        AMEvent();
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
    }
}