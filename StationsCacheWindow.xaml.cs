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

        public async Task GrabStations()
        {
            await new Search().GetAllStations();
        }
    }
}
