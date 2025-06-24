using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static AMDiscordRPC.Globals;
using static AMDiscordRPC.S3;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace AMDiscordRPC.UIComponents
{
    /// <summary>
    /// Interaction logic for InputWindow.xaml
    /// </summary>
    public partial class InputWindow : Window
    {
        private static InputWindow Instance;

        public InputWindow()
        {
            InitializeComponent();
            Instance = this;
            ChangeS3Status(S3Status);
        }

        private void button_Click(object sender, RoutedEventArgs e)
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
                    if (res != -1) { 
                        S3_Credentials = creds;
                        MessageBox.Show("S3 Credentials Successfully Added to Database");
                        InitS3();
                    }
                    else MessageBox.Show("An error happened while inserting to database.");
                }
                else
                {
                    var res = Database.ExecuteNonQueryCommand($"UPDATE creds SET ({string.Join(", ", Regex.Matches(Database.sqlMap["creds"], @"S3_\w+").FilterRepeatMatches())}) = ({string.Join(", ", creds.GetNotNullValues())})");
                    if (res != -1) {
                        S3_Credentials = creds;
                        MessageBox.Show("S3 Credentials Successfully Updated");
                        InitS3();
                    }
                    else MessageBox.Show("An error happened while updating the database.");
                }
            }
        }

        public static void ChangeS3Status(S3ConnectionStatus value)
        {
            if (Instance == null) return;

            switch (value) { 
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
    }
}
