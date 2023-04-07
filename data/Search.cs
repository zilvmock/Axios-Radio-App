using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using System.Threading.Tasks;
using ABI.Windows.Storage.BulkAccess;
using Newtonsoft.Json;
using System.Text.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;
using System.Xml.Linq;
using Axios.Data;

namespace Axios.data
{
    internal class Search
    {
        private static string API_URL = string.Empty;

        public string SearchPhrase { get; set; }

        public Search()
        {
            if (!File.Exists(Resources.CACHE_FILE_PATH)) { File.Create(Resources.CACHE_FILE_PATH); }
            API_URL = API.GetRadioBrowserApiUrl();
        }

        public async Task GetAllStations()
        {
            using HttpClient httpClient = new HttpClient();
            using HttpResponseMessage response = await httpClient.GetAsync($"http://{API_URL}/json/stations?hidebroken=true&limit=500000");

            using Stream stream = await response.Content.ReadAsStreamAsync();
            using StreamReader reader = new StreamReader(stream);

            using JsonTextReader jsonReader = new JsonTextReader(reader);
            while (await jsonReader.ReadAsync())
            {
                if (jsonReader.TokenType == JsonToken.StartArray)
                {
                    await ParseJArrayToStationsList(jsonReader);
                }
            }

            response.Dispose();
            httpClient.Dispose();
            _ = stream.DisposeAsync();
            reader.Dispose();
            jsonReader.Close();
        }

        private async Task ParseJArrayToStationsList(JsonTextReader jsonReader)
        {
            List<Tuple<string, string, string, string, int, string>> namesList = new();

            while (await jsonReader.ReadAsync())
            {
                if (jsonReader.TokenType == JsonToken.StartObject)
                {
                    Tuple<string, string, string, string, int, string> tuple = await ParseJsonObjectToTuple(jsonReader);
                    if (tuple != null && !string.IsNullOrEmpty(tuple.Item2))
                    {
                        namesList.Add(tuple);
                    }
                }
                else if (jsonReader.TokenType == JsonToken.EndArray)
                {
                    break;
                }
            }

            await SaveToJson(namesList.Distinct().ToList());
        }
        private async Task<Tuple<string, string, string, string, int, string>> ParseJsonObjectToTuple(JsonTextReader jsonReader)
        {
            string url = "";
            string stationName = "";
            string icon = "";
            string country = "";
            int votes = 0;
            string uuid = "";

            while (await jsonReader.ReadAsync())
            {
                if (jsonReader.TokenType == JsonToken.PropertyName)
                {
                    string propertyName = jsonReader.Value.ToString();
                    await jsonReader.ReadAsync();

                    switch (propertyName)
                    {
                        case "url_resolved":
                            url = jsonReader.Value.ToString();
                            break;
                        case "name":
                            stationName = jsonReader.Value.ToString();
                            break;
                        case "favicon":
                            icon = jsonReader.Value.ToString();
                            break;
                        case "countrycode":
                            country = jsonReader.Value.ToString();
                            break;
                        case "votes":
                            int.TryParse(jsonReader.Value.ToString(), out votes);
                            break;
                        case "stationuuid":
                            uuid = jsonReader.Value.ToString();
                            break;
                        default:
                            break;
                    }
                }
                else if (jsonReader.TokenType == JsonToken.EndObject)
                {
                    break;
                }
            }

            return new Tuple<string, string, string, string, int, string>(url, stationName, icon, country, votes, uuid);
        }

        private async Task SaveToJson(List<Tuple<string, string, string, string, int, string>> list)
        {
            await Task.Run(() =>
            {
                string json = JsonConvert.SerializeObject(list, Formatting.Indented);
                File.WriteAllText(Resources.CACHE_FILE_PATH, json);
            });
            Resources.EnforceClean();
        }

        private static Tuple<string, string, string, string, int, string> ReadStation(JsonTextReader reader)
        {
            var station = new Tuple<string, string, string, string, int, string>("", "", "", "", 0, "");

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.PropertyName)
                {
                    var propertyName = (string)reader.Value;

                    switch (propertyName)
                    {
                        case "Item1":
                            station = new Tuple<string, string, string, string, int, string>(
                                reader.ReadAsString(), station.Item2, station.Item3, station.Item4, station.Item5, station.Item6);
                            break;
                        case "Item2":
                            station = new Tuple<string, string, string, string, int, string>(
                                station.Item1, reader.ReadAsString(), station.Item3, station.Item4, station.Item5, station.Item6);
                            break;
                        case "Item3":
                            station = new Tuple<string, string, string, string, int, string>(
                                station.Item1, station.Item2, reader.ReadAsString(), station.Item4, station.Item5, station.Item6);
                            break;
                        case "Item4":
                            station = new Tuple<string, string, string, string, int, string>(
                                station.Item1, station.Item2, station.Item3, reader.ReadAsString(), station.Item5, station.Item6);
                            break;
                        case "Item5":
                            station = new Tuple<string, string, string, string, int, string>(
                                station.Item1, station.Item2, station.Item3, station.Item4, (int)reader.ReadAsInt32(), station.Item6);
                            break;
                        case "Item6":
                            station = new Tuple<string, string, string, string, int, string>(
                                station.Item1, station.Item2, station.Item3, station.Item4, station.Item5, reader.ReadAsString());
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }
                else if (reader.TokenType == JsonToken.EndObject)
                {
                    break;
                }
            }

            return station;
        }

        public List<Tuple<string, string, string, string, int, string>> GetByName()
        {
            var stations = new List<Tuple<string, string, string, string, int, string>>();

            using (var reader = new JsonTextReader(File.OpenText(Resources.CACHE_FILE_PATH)))
            {
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.StartObject)
                    {
                        var station = ReadStation(reader);

                        if (station.Item2.Contains(SearchPhrase))
                        {
                            stations.Add(station);
                        }
                    }
                }
            }

            return stations;
        }

        public List<Tuple<string, string, string, string, int, string>> GetByVotes()
        {
            var stations = new List<Tuple<string, string, string, string, int, string>>();

            using (var reader = new JsonTextReader(File.OpenText(Resources.CACHE_FILE_PATH)))
            {
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.StartObject)
                    {
                        var station = ReadStation(reader);
                        stations.Add(station);
                    }
                }
            }

            return stations.OrderByDescending(s => s.Item5).Take(100).ToList();
        }

        public List<Tuple<string, string, string, string, int, string>> GetPageOfStations(int from, int to, List<Tuple<string, string, string, string, int, string>> stations)
        {
            List<Tuple<string, string, string, string, int, string>> stationsInRange = new();
            
            for (int i = from; i < to && i < stations.Count; i++)
            {
                Tuple<string, string, string, string, int, string> station = stations[i];
                stationsInRange.Add(station);
            }

            return stationsInRange;
        }
    }
}
