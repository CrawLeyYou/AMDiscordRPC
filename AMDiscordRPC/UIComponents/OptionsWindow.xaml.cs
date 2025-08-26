
using System.Management.Instrumentation;
using System.Windows;

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

            };
        }
    }
}
