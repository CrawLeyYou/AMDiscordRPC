
using System;
using System.Management.Instrumentation;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using static AMDiscordRPC.Globals;

namespace AMDiscordRPC.UIComponents
{
    /// <summary>
    /// Interaction logic for OptionsWindow.xaml
    /// </summary>
    public partial class OptionsWindow : Window
    {
        private static OptionsWindow Instance;
        public OptionsWindow()
        {
            InitializeComponent();
            Instance = this;
            Instance.Loaded += (s, e) =>
            {
                smallImage.SelectedIndex = (int)SelectedSmallImage;
            };
        }

        private void SmallImage_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedSmallImage = (SmallImage)smallImage.SelectedIndex;
            if (Database.ExecuteScalarCommand("SELECT smallImage FROM clientSettings") == null)
                Database.ExecuteNonQueryCommand($"INSERT INTO clientSettings (smallImage) VALUES ({smallImage.SelectedIndex})");
            else
                Database.ExecuteNonQueryCommand($"UPDATE clientSettings SET (smallImage) = ({smallImage.SelectedIndex})");
        }
    }
}
