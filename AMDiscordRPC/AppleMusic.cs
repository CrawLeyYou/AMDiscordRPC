using FlaUI.Core;
using System;
using static AMDiscordRPC.Globals;

namespace AMDiscordRPC
{
    internal class AppleMusic
    {
        public static void AttachToAM()
        {
            try
            {
                AppleMusicProc = Application.Attach("AppleMusic.exe");
                AMAttached = true;
                log.Info($"Attached to PID: {AppleMusicProc.ProcessId}");
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
