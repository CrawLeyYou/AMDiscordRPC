﻿using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System;
using System.Linq;
using System.Threading;
using System.Web;
using static AMDiscordRPC.AppleMusic;
using static AMDiscordRPC.Discord;
using static AMDiscordRPC.Globals;
using static AMDiscordRPC.Covers;

namespace AMDiscordRPC
{
    internal class AMDiscordRPC
    {
        private static string oldAlbumnArtist;
        static void Main(string[] args)
        {
            ConfigureLogger();
            InitializeDiscordRPC();
            AttachToAppleMusic();
            AMSongDataEvent.SongChanged += async (sender, x) =>
             {
                 log.Info($"Song: {x.SongName} \\ Artist and Album: {x.ArtistandAlbumName}");
                 if (x.ArtistandAlbumName == oldAlbumnArtist && oldData.Assets.LargeImageKey != null)
                 {
                     SetPresence(x);
                 }
                 else
                 {
                     string[] resp = await FetchiTunes(HttpUtility.UrlEncode(ConvertToValidString(x.ArtistandAlbumName) + $" {ConvertToValidString(x.SongName)}"));
                     SetPresence(x, resp);
                     oldAlbumnArtist = x.ArtistandAlbumName;
                 }
             };
            AMEvent();
        }

        static void AMEvent()
        {
            using (var automation = new UIA3Automation())
            {
                var playingStatus = false;
                AutomationElement parent = null;
                AutomationElement[] listeningInfo = null;
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
                        listeningInfo = parent.FindFirstChild(cf => cf.ByAutomationId("LCD")).FindAllChildren().Where(x => (x.ControlType == ControlType.Pane)).ToArray();
                        slider = parent.FindFirstChild(cf => cf.ByAutomationId("LCD")).FindFirstChild(cf => cf.ByAutomationId("LCDScrubber"));
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
                    bool resetStatus = false;
                    double oldValue = 0;

                    while (true)
                    {
                        if (AppleMusicProc.HasExited != true)
                        {
                            try
                            {
                                var currentSong = listeningInfo[0].Name;
                                var currentArtistAlbum = listeningInfo[1].Name;
                                var dashSplit = listeningInfo[1].Name.Split('-');
                                if (oldValue == 0) oldValue = slider.AsSlider().Value;
                                DateTime currentTime = DateTime.UtcNow;
                                DateTime startTime = currentTime.Subtract(TimeSpan.FromSeconds(slider.AsSlider().Value + 1));
                                DateTime endTime = currentTime.AddSeconds(slider.AsSlider().Maximum).Subtract(TimeSpan.FromSeconds(slider.AsSlider().Value + 1));
                                bool isSingle = dashSplit[dashSplit.Length - 1].Contains("Single");

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
                                else if (resetStatus == false && slider.AsSlider().Maximum != 0 && oldValue != 0 && currentSong == previousSong && currentArtistAlbum == previousArtistAlbum)
                                {
                                    ChangeTimestamps(startTime, endTime);
                                    oldValue = slider.AsSlider().Value;
                                }

                                if (slider.AsSlider().Maximum != 0 && slider.AsSlider().Value != 0 && endTime != startTime && (currentSong != previousSong || currentArtistAlbum != previousArtistAlbum))
                                {
                                    // sometimes discord doesn't register rich presence idk why i tried everything...
                                    previousArtistAlbum = currentArtistAlbum;
                                    previousSong = currentSong;
                                    oldValue = 0;
                                    AMSongDataEvent.SongChange(new SongData(currentSong, (isSingle) ? string.Join("-", dashSplit.Take(dashSplit.Length - 1).ToArray()) : currentArtistAlbum, currentArtistAlbum.Split('—').Length <= 1, startTime, endTime));
                                }

                                if (playButton?.Name != null && (localizedPlay != null && localizedPlay == playButton?.Name || localizedStop != null && localizedStop != playButton?.Name))
                                {
                                    localizedPlay = playButton.Name;
                                    client.ClearPresence();
                                    resetStatus = true;
                                }
                                else if (resetStatus == true && playButton?.Name != null && localizedPlay != null && localizedPlay != playButton?.Name && slider.AsSlider().Maximum != 0)
                                {
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
                        while (!AMAttached) {
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