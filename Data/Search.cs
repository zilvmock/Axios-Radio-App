using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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
using System.Text.RegularExpressions;

namespace Axios.data
{
    public class Search
    {
        private static string API_URL;
        public DateTime LastVoteTime { get; set; }
        public StringCollection LastVoteUUIDs { get; set; }

        public string SearchPhrase { get; set; }

        public Search()
        {
            if (!File.Exists(Resources.CACHE_FILE_PATH)) { File.Create(Resources.CACHE_FILE_PATH); }

            API_URL = API.GetRadioBrowserApiUrl();
            LastVoteTime = DateTime.MinValue;
            LastVoteUUIDs = new();
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
            await stream.DisposeAsync();
            reader.Dispose();
            jsonReader.Close();
            Resources.EnforceClean();
        }

        private async Task ParseJArrayToStationsList(JsonTextReader jsonReader)
        {
            List<Tuple<string, string, string, string, int, string>> namesList = new();

            while (await jsonReader.ReadAsync())
            {
                if (jsonReader.TokenType == JsonToken.StartObject)
                {
                    Tuple<string, string, string, string, int, string> tuple = await ParseJsonObjectToTuple(jsonReader);
                    if (tuple != null && !string.IsNullOrWhiteSpace(tuple.Item2) && !string.IsNullOrEmpty(tuple.Item2))
                    {
                        string modifiedItem2 = tuple.Item2.TrimStart('\t').Replace("\n", "").Replace("\t", "");
                        modifiedItem2 = Regex.Replace(modifiedItem2, @"\s{3,}", " ").Trim();
                        var tempTuple = new Tuple<string, string, string, string, int, string>(
                            tuple.Item1,
                            modifiedItem2,
                            tuple.Item3,
                            tuple.Item4,
                            tuple.Item5,
                            tuple.Item6
                        );
                        namesList.Add(tempTuple);
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

        public async Task CountStationClick(string uuid)
        {
            if (string.IsNullOrEmpty(uuid)) { return; }
            await new HttpClient().GetAsync($"http://{API_URL}/json/url/{uuid}");
        }

        public async Task VoteForStation(string uuid)
        {
            if (string.IsNullOrEmpty(uuid)) { return; }
            if (DateTime.Now - LastVoteTime < TimeSpan.FromMinutes(10) && LastVoteUUIDs.Contains(uuid)) { return; }
            LastVoteTime = DateTime.Now;
            LastVoteUUIDs.Add(uuid);
            await new HttpClient().GetAsync($"http://{API_URL}/json/vote/{uuid}");

            // Update votes in cache file
            await Task.Run(() =>
            {
                JObject targetObject = null;
                JArray jsonArray = null;
                using (var fileStream = new FileStream(Resources.CACHE_FILE_PATH, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var streamReader = new StreamReader(fileStream))
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    jsonArray = JArray.Load(jsonReader);
                    foreach (JObject obj in jsonArray.OfType<JObject>())
                    {
                        string fileUUID = obj["Item6"].ToString();
                        if (uuid != fileUUID) continue;
                        targetObject = obj;
                        break;
                    }
                }

                if (targetObject != null)
                {
                    int newVoteCount = (int)targetObject["Item5"] + 1;
                    targetObject["Item5"] = newVoteCount;

                    for (int i = 0; i < jsonArray.Count; i++)
                    {
                        if (jsonArray[i] == targetObject)
                        {
                            jsonArray[i] = targetObject;
                            break;
                        }
                    }

                    using (var fileStreamWrite = new FileStream(Resources.CACHE_FILE_PATH, FileMode.Create, FileAccess.Write))
                    using (var streamWriter = new StreamWriter(fileStreamWrite))
                    using (var jsonWriter = new JsonTextWriter(streamWriter))
                    {
                        jsonWriter.Formatting = Formatting.Indented;
                        jsonArray.WriteTo(jsonWriter);
                    }
                }

                Resources.EnforceClean();
            });
        }
    }
}
