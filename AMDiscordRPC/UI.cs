using AMDiscordRPC.UIComponents;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using static AMDiscordRPC.Database;
using static AMDiscordRPC.Globals;
using Application = System.Windows.Application;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace AMDiscordRPC
{
    internal class UI
    {
        private static InputWindow inputWindow;
        private static Application app;
        private static Thread mainThread = Thread.CurrentThread;

        public static void CreateUI()
        {
            Thread thread = new Thread(() =>
            {
                new AMDiscordRPCTray();
                app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                app.Run();
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        public static void FFmpegDialog()
        {
            log.Warn("FFmpeg not found");
            Thread thread = new Thread(() =>
            {
                if (System.Windows.MessageBox.Show("FFmpeg not found. Do you want to specify the path?", "FFmpeg not found.", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.Yes)
                {
                    log.Debug("Yes");
                    OpenFileDialog openFileDialog = new OpenFileDialog();
                    openFileDialog.FileName = "ffmpeg";
                    openFileDialog.DefaultExt = ".exe";
                    openFileDialog.Filter = "Executables (.exe)|*.exe";
                    openFileDialog.Multiselect = false;
                    bool? openFileDialogRes = openFileDialog.ShowDialog();

                    if (openFileDialogRes == true)
                    {
                        ffmpegPath = Path.GetDirectoryName(openFileDialog.FileName);
                        if (ExecuteScalarCommand($"SELECT FFmpegPath from creds") != null)
                            ExecuteNonQueryCommand($"UPDATE creds SET FFmpegPath = '{ffmpegPath}'");
                        else
                            ExecuteNonQueryCommand($"INSERT INTO creds (FFmpegPath) VALUES ('{ffmpegPath}')");
                    }
                }
                else log.Debug("No");
            });
            /* 
             * Dude why we need STA for simple OpenFileDialog 
             * Update: Oh it was because Microsoft is lazy ass company that still uses COM which designed in 1990 and dont want to implement special support for older softwares and instead they force us to use STA :D
            */
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
                         new MenuItem("Show Latest Log", (s,e)  => { Process.Start("notepad", $"{Path.Combine(Directory.GetCurrentDirectory(), @"logs\latest.log")}"); }),
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
