using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using static AMDiscordRPC.Globals;
using static AMDiscordRPC.Helpers.TextBoxHelper;
using static AMDiscordRPC.S3;
using Brushes = System.Windows.Media.Brushes;

namespace AMDiscordRPC.UIComponents
{
    /// <summary>
    /// Interaction logic for InputWindow.xaml
    /// </summary>
    public partial class InputWindow : Window
    {
        private static InputWindow Instance;
        private enum ShowMode { Show, Hide }
        private static ShowMode currentShowMode = ShowMode.Hide;

        public InputWindow()
        {
            InitializeComponent();
            Instance = this;
            ChangeS3Status(S3Status);
            Instance.Loaded += (s, e) =>
            {
                if (S3_Credentials != null)
                    PutValues(S3_Credentials);
            };
        }

        private void SetS3Button_Click(object sender, RoutedEventArgs e)
        {
            S3_Creds creds = new S3_Creds(
                (AccessKeyIDBox.Text != "") ? AccessKeyIDBox.Text : null,
                (SecretKeyBox.Text != "") ? SecretKeyBox.Text : null,
                (EndpointBox.Text != "") ? EndpointBox.Text : null,
                (BucketNameBox.Text != "") ? BucketNameBox.Text : null,
                (PublicBucketURLBox.Text != "") ? PublicBucketURLBox.Text : null,
                IsSpecificKeyBox.IsChecked
            );
            if (creds.GetNullKeys().Count != 0)
            {
                MessageBox.Show($"It looks like you missed to add these credentials: {string.Join(", ", creds.GetNullKeys())}");
            }
            else
            {
                if (Database.ExecuteScalarCommand("SELECT S3_accessKey FROM creds") == null)
                {
                    var res = Database.ExecuteNonQueryCommand($"INSERT INTO creds ({string.Join(", ", Regex.Matches(Database.sqlMap["creds"], @"S3_\w+").FilterRepeatMatches())}) VALUES ({string.Join(", ", creds.GetNotNullValues())})");
                    if (res != -1)
                    {
                        S3_Credentials = creds;
                        MessageBox.Show("S3 Credentials Successfully Added to Database");
                        InitS3();
                    }
                    else MessageBox.Show("An error happened while inserting to database.");
                }
                else
                {
                    var res = Database.ExecuteNonQueryCommand($"UPDATE creds SET ({string.Join(", ", Regex.Matches(Database.sqlMap["creds"], @"S3_\w+").FilterRepeatMatches())}) = ({string.Join(", ", creds.GetNotNullValues())})");
                    if (res != -1)
                    {
                        S3_Credentials = creds;
                        MessageBox.Show("S3 Credentials Successfully Updated");
                        InitS3();
                    }
                    else MessageBox.Show("An error happened while updating the database.");
                }
            }
        }

        private static void PutValues(S3_Creds creds, ShowMode mode = ShowMode.Hide)
        {
            List<TextBox> Instances = new List<TextBox> { Instance.AccessKeyIDBox, Instance.SecretKeyBox, Instance.EndpointBox, Instance.BucketNameBox, Instance.PublicBucketURLBox };
            List<string> Keys = new List<string>() { creds.accessKey, creds.secretKey, creds.serviceURL, creds.bucketName, creds.bucketURL };
            foreach (var (item, index) in Instances.Select((v, i) => (v, i)))
            {
                PlaceholderAdorner adorner = Helpers.TextBoxHelper.GetPlaceholderAdorner(item);
                item.Text = (mode == ShowMode.Show) ? Keys[index] : new string('*', Keys[index].Length);
                item.IsEnabled = (mode == ShowMode.Show) ? true : false;
                if (Keys[index].Length > 0)
                    adorner.Visibility = Visibility.Hidden;
                else
                    adorner.Visibility = Visibility.Visible;
            }
            Instance.IsSpecificKeyBox.IsChecked = creds.isSpecificKey;
            Instance.ShowButton.Content = (mode == ShowMode.Show) ? "Hide" : "Show";
            Instance.SetS3Button.IsEnabled = (mode == ShowMode.Show) ? true : false;
            currentShowMode = mode;
        }

        public static void ChangeS3Status(S3ConnectionStatus value)
        {
            if (Instance == null) return;
            switch (value)
            {
                case S3ConnectionStatus.Connected:
                    Instance.S3Connection.Text = "Connected";
                    Instance.S3Connection.Foreground = Brushes.Green;
                    break;
                case S3ConnectionStatus.Disconnected:
                    Instance.S3Connection.Text = "Disconnected";
                    Instance.S3Connection.Foreground = Brushes.Black;
                    break;
                case S3ConnectionStatus.Error:
                    Instance.S3Connection.Text = "Error";
                    Instance.S3Connection.Foreground = Brushes.Red;
                    break;
            }
        }

        private void ShowButton_Click(object sender, RoutedEventArgs e)
        {
            if (S3_Credentials != null)
                PutValues(S3_Credentials, (currentShowMode == ShowMode.Show) ? ShowMode.Hide : ShowMode.Show);
        }
    }
}
