using System;
using System.Threading;
using System.Windows;

namespace AMDiscordRPC
{
    internal class UI
    {
        public static void CreateUI()
        {
            var thread = new Thread(() =>
            {
                var app = new Application();
                app.Run(new UIComponents.InputWindow());
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
    }
}
