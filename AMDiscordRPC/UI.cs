using AMDiscordRPC.UIComponents;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace AMDiscordRPC
{
    internal class UI
    {
        private static InputWindow inputWindow;
        private static Application app;
        private static Thread mainThread = Thread.CurrentThread;

        public static void CreateUI()
        {
            var thread = new Thread(() =>
            {
                new AMDiscordRPCTray();
                app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                app.Run();
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        public class AMDiscordRPCTray
        {
            private static NotifyIcon notifyIcon = new NotifyIcon();
            private static ContextMenu contextMenu = new ContextMenu();
            public static MenuItem notifySongState = new MenuItem();
            public MenuItem s3Menu = new MenuItem();

            public AMDiscordRPCTray()
            {
                notifySongState.Text = "AMDiscordRPC";
                notifySongState.Index = 0;
                notifySongState.Enabled = false;

                s3Menu.Text = "S3 Credentials";
                s3Menu.Index = 1;
                s3Menu.Click += new EventHandler((object sender, EventArgs e) =>
                {
                    app.Dispatcher.Invoke(() =>
                    {
                        inputWindow = new InputWindow();
                        inputWindow.Show();
                    });
                });

                contextMenu.MenuItems.AddRange(
                     new MenuItem[]
                     {
                         notifySongState,
                         s3Menu,
                         new MenuItem("Exit", (s, e) => { Environment.Exit(0); })
                     }
                );

                notifyIcon.Icon = new Icon("MacOS_Big_Sur_logo.ico");
                notifyIcon.ContextMenu = contextMenu;
                notifyIcon.Text = "AMDiscordRPC";
                notifyIcon.Visible = true;
            }

            public static void ChangeSongState(string stateText)
            {
                notifySongState.Text = stateText;

                notifyIcon.ContextMenu = null;
                notifyIcon.ContextMenu = contextMenu;
            }
        }
    }
}
