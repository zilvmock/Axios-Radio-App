using System.Windows.Controls;
using System.Windows.Input;

namespace Axios.Controls
{
    public partial class SidePanel : UserControl
    {
        private static bool IsSettingsShowing { get; set; } = false;
        private static bool IsRadioShowing { get; set; } = true;

        private readonly MainWindow _mainWindow;

        public SidePanel(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
        }

        private void RadioStationsBtn_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsRadioShowing) { return; }
            IsSettingsShowing = !IsSettingsShowing;
            IsRadioShowing = !IsRadioShowing;
            _mainWindow.MWContentFrame.Content = MainWindow.RadioPage;
            MainWindow.RadioPage.RefreshPageItems();
        }

        private void SettingsBtn_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsSettingsShowing) { return; }
            IsSettingsShowing = !IsSettingsShowing;
            IsRadioShowing = !IsRadioShowing;
            _mainWindow.MWContentFrame.Content = MainWindow.SettingsPage;
        }
    }
}
