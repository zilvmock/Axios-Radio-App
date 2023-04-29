using Axios.data;
using System.Collections.Generic;
using System.Threading.Tasks;
using Axios.Models;

namespace Axios.Services
{
    public class RadioStationManagerService
    {
        private readonly RadioStationManager _radioStationManager;

        public RadioStationManagerService(RadioStationManager radioStationManager)
        {
            _radioStationManager = radioStationManager;
        }

        public async Task GetAllStationsAsync()
        {
            await _radioStationManager.GetAllStations();
        }

        public async Task<List<Station>> GetStationsByNameAsync(string searchPhrase)
        {
            return await _radioStationManager.GetStationsByNameAsync(searchPhrase);
        }

        public async Task<List<Station>> GetStationsByVotesAsync()
        {
            return await _radioStationManager.GetStationsByVotesAsync();
        }

        public async Task<List<Station>> GetPageOfStationsAsync(int from, int to, List<Station> stations)
        {
           return await _radioStationManager.GetPageOfStationsAsync(from, to, stations);
        }
    }
}
