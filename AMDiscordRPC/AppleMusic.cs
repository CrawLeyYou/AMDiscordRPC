using FlaUI.Core;
using System;
using static AMDiscordRPC.Globals;

namespace AMDiscordRPC
{
    internal class AppleMusic
    {
        public static void AttachToAppleMusic()
        {
            try
            {
                AppleMusicProc = Application.Attach("AppleMusic.exe");
                AMAttached = true;
                log.Info($"Attached to Process Id: {AppleMusicProc.ProcessId}");
            }
            catch (Exception e)
            {
                log.Debug($"Apple Music not found: {e.Message}");
                AMAttached = false;
                client.ClearPresence();
            }
        }
    }
}
