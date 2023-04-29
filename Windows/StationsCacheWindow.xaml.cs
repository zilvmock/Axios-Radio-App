using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Axios.Pages;
using Axios.Properties;

namespace Axios.Windows
{
    public partial class StationsCacheWindow : Window
    {
        public StationsCacheWindow() { InitializeComponent(); }

        public async Task InitializeStationsCache()
        {
            if (!File.Exists(Data.Resources.CacheFilePath) || Settings.Default.FirstLaunch)
            {
                if (Application.Current.MainWindow == null) { throw new Exception("Cannot access the main application window."); }
                Show();
                Application.Current.MainWindow.IsEnabled = false;
                Application.Current.MainWindow.Opacity = 0.5;
                await RadioPage.RadioStationManagerService.GetAllStationsAsync();
                Application.Current.MainWindow.IsEnabled = true;
                Application.Current.MainWindow.Opacity = 1;
                Settings.Default.FirstLaunch = false;
                Close();
            }
        }
    }
}
