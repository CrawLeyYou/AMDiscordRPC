using System.Text.RegularExpressions;
using System.Windows;
using static AMDiscordRPC.Globals;

namespace AMDiscordRPC.UIComponents
{
    /// <summary>
    /// Interaction logic for InputWindow.xaml
    /// </summary>
    public partial class InputWindow : Window
    {
        public InputWindow()
        {
            InitializeComponent();
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
                if (Database.ExecuteScalarCommand("SELECT * FROM creds") == null)
                {
                    var res = Database.ExecuteNonQueryCommand($"INSERT INTO creds ({string.Join(", ", Regex.Matches(Database.sqlMap["creds"], @"S3_\w+").FilterRepeatMatches())}) VALUES ({string.Join(", ", creds.GetNotNullValues())})");
                    if (res != -1) MessageBox.Show("S3 Credentials Successfully Added to Database");
                    else MessageBox.Show("An error happened while inserting to database.");
                }
                else
                {
                    var res = Database.ExecuteNonQueryCommand($"UPDATE creds SET ({string.Join(", ", Regex.Matches(Database.sqlMap["creds"], @"S3_\w+").FilterRepeatMatches())}) = ({string.Join(", ", creds.GetNotNullValues())})");
                    if (res != -1) MessageBox.Show("S3 Credentials Successfully Updated");
                    else MessageBox.Show("An error happened while updating the database.");
                }
            }
        }
    }
}
