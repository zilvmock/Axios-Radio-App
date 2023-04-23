using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Axios.data;

namespace Axios
{
    public partial class StationsCacheWindow : Window
    {
        public StationsCacheWindow()
        {
            InitializeComponent();
        }

        public async Task InitializeStationsCache()
        {
            if (!File.Exists(Data.Resources.CACHE_FILE_PATH) || MainWindow.AppSettings.FirstLaunch)
            {
                Show();
                Application.Current.MainWindow.IsEnabled = false;
                Application.Current.MainWindow.Opacity = 0.5;
                await new Search().GetAllStations();
                Application.Current.MainWindow.IsEnabled = true;
                Application.Current.MainWindow.Opacity = 1;
                MainWindow.AppSettings.FirstLaunch = false;
                Close();
            }
        }
    }
}
