using System.Windows.Controls;
using System.Windows.Input;

namespace Axios
{
    public partial class SidePanel : UserControl
    {
        private static bool IsSettingsShowing { get; set; } = false;
        private static bool IsRadioShowing { get; set; } = true;

        private readonly MainWindow _mainWindow;

        public SidePanel(MainWindow mainWindow)
        {
            InitializeComponent();
            this._mainWindow = mainWindow;
        }

        private void RadioStationsBtn_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsRadioShowing == false)
            {
                IsSettingsShowing = !IsSettingsShowing;
                IsRadioShowing = !IsRadioShowing;
                _mainWindow.MWContentFrame.Content = MainWindow.RadioPage;
            }
        }

        private void SettingsBtn_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsSettingsShowing == false)
            {
                IsSettingsShowing = !IsSettingsShowing;
                IsRadioShowing = !IsRadioShowing;
                _mainWindow.MWContentFrame.Content = MainWindow.SettingsPage;
            }
        }
    }
}
