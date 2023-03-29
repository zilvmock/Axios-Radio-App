using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Axios
{
    public partial class SidePanel : UserControl
    {
        private static bool IsSettingsShowing { get; set; } = false;
        private static bool IsRadioShowing { get; set; } = true;

        public static RadioPage RP { get; set; } = new();
        public static SettingsPage SP { get; set; } = new();

        public SidePanel()
        {
            InitializeComponent();
        }

        private void RadioStationsBtn_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsRadioShowing == false)
            {
                IsSettingsShowing = !IsSettingsShowing;
                IsRadioShowing = !IsRadioShowing;
                // TODO: doesn't apply coloring
                Window mw = Application.Current.MainWindow;
                mw.Content = RP;
                _ = RP.UpdateStationBackgroundToCorrect(RadioPage.CurrentStationRow);
            }
        }

        private void SettingsBtn_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsSettingsShowing == false)
            {
                IsSettingsShowing = !IsSettingsShowing;
                IsRadioShowing = !IsRadioShowing;
                Window mw = Application.Current.MainWindow;
                mw.Content = SP;
            }
        }
    }
}
