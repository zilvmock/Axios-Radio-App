using Axios.data;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Axios.Services
{
    public class RadioStationService
    {
        private readonly RadioStationManager _radioStationManager;

        public RadioStationService(RadioStationManager radioStationManager)
        {
            _radioStationManager = radioStationManager;
        }

        public async Task VoteForStationAsync(string uuid)
        {
            await _radioStationManager.VoteForStationAsync(uuid);
        }

        public async Task<BitmapImage> GetStationIconAsync(string artURL)
        {
            return await _radioStationManager.GetStationIconAsync(artURL);
        }

        public async Task CountStationClickAsync(string uuid)
        {
            await _radioStationManager.CountStationClickAsync(uuid);
        }
    }
}
