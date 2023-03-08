using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Axios.data
{
    internal class Search
    {
        private static string API_URL = string.Empty;

        public Search() { if (API_URL == string.Empty) API_URL = API.GetRadioBrowserApiUrl(); }

        public async Task<List<Tuple<string, string, string, string, int>>> GetByName(string name)
        {
            using HttpClient httpClient = new HttpClient();
            HttpResponseMessage response = await httpClient.GetAsync($"http://{API_URL}/json/stations/byname/{name}");

            string content = await response.Content.ReadAsStringAsync();
            JArray jsonArray = JArray.Parse(content);

            return GetStationsList(jsonArray);
        }

        public async Task<List<Tuple<string, string, string, string, int>>> GetByVotes()
        {
            using HttpClient httpClient = new HttpClient();
            HttpResponseMessage response = await httpClient.GetAsync($"http://{API_URL}/json/stations/topvote?limit=100");

            string content = await response.Content.ReadAsStringAsync();
            JArray jsonArray = JArray.Parse(content);

            return GetStationsList(jsonArray);
        }

        private List<Tuple<string, string, string, string, int>> GetStationsList(JArray jsonArray)
        {
            List<Tuple<string, string, string, string, int>> namesList = new List<Tuple<string, string, string, string, int>>();
            foreach (var jsonObject in jsonArray)
            {
                if (namesList.Count > 499) { break; }

                string url = jsonObject["url_resolved"]?.ToString() ?? "";
                string stationName = jsonObject["name"]?.ToString() ?? "";
                string icon = jsonObject["favicon"]?.ToString() ?? "";
                string country = jsonObject["countrycode"]?.ToString() ?? "";
                int votes = int.Parse(jsonObject["votes"]?.ToString() ?? "");

                if (!string.IsNullOrEmpty(stationName))
                {
                    namesList.Add(new Tuple<string, string, string, string, int>(url, stationName, icon, country, votes));
                }
            }

            return namesList.Distinct().ToList();
        }
    }
}
