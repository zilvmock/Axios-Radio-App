using System.Windows;
using System.Windows.Controls;
using Axios.Windows;

namespace Axios.Pages
{
    public partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
            MinimizeOnCloseCheckBox.IsChecked = MainWindow.AppSettings.MinimizeOnExit;
        }

        private void SaveSettingsBtn_OnClick(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.MinimizeOnExit = MinimizeOnCloseCheckBox.IsChecked.HasValue ? (bool)MinimizeOnCloseCheckBox.IsChecked : false;
            Properties.Settings.Default.Save();
        }

        private async void UpdateStationsCacheBtn_OnClick(object sender, RoutedEventArgs e)
        {
            Data.Resources.ClearTempDir(true);
            await new StationsCacheWindow().InitializeStationsCache();
            await RadioPage.RadioStationManagerService.GetAllStationsAsync();
        }
    }
}
