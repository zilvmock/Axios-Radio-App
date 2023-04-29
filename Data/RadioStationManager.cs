using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Axios.Data;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using Axios.Models;

namespace Axios.data
{
    /// <summary>
    /// A class for accessing and managing radio stations locally from a remote API.
    /// </summary>
    /// <remarks>
    /// This class has the following known issues:
    /// - Parts of the code that interact with JSON data allocate a significant amount of memory to the heap.
    /// </remarks>
    public class RadioStationManager
    {
        public DateTime LastVoteTime { get; set; }
        public StringCollection LastVoteUUIDs { get; set; }

        public RadioStationManager()
        {
            LastVoteTime = DateTime.MinValue;
            LastVoteUUIDs = new();
        }

        /// <summary>
        /// Retrieves the station icon from a given URL and returns it as a <see cref="BitmapImage"/>.
        /// </summary>
        /// <param name="artURL">The URL of the station icon to retrieve.</param>
        /// <returns>A <see cref="BitmapImage"/> of the station icon, or the default logo if the icon cannot be retrieved.</returns>
        public async Task<BitmapImage> GetStationIconAsync(string artURL)
        {
            BitmapImage logoImg = new(new Uri("/Assets/logo.png", UriKind.Relative));
            if (string.IsNullOrEmpty(artURL)) { return logoImg; }
            try
            {
                HttpClient client = new();
                MemoryStream stream = new(await client.GetByteArrayAsync(artURL));
                string tempFilePath = Path.Combine(Resources.TempFolderPath, "stationFavIcon_" + DateTime.Now.Ticks + Path.GetExtension(artURL));
                await using (FileStream fileStream = new(tempFilePath, FileMode.Create)) { stream.WriteTo(fileStream); }

                BitmapImage image = new();
                image.BeginInit();
                image.UriSource = new Uri(tempFilePath);
                image.EndInit();
                return image;
            }
            catch (Exception) { return logoImg; }
        }

        /// <summary>
        /// Asynchronously retrieves a list of radio stations by search phrase from a local cache file.
        /// </summary>
        /// <param name="searchPhrase">The search phrase to filter the radio stations by.</param>
        /// <returns>A list of radio stations that match the search phrase.</returns>
        public async Task<List<Station>> GetStationsByNameAsync(string searchPhrase)
        {
            List<Station> stations = new();
            bool tryAgain = true;
            int tries = 0;

            while (tryAgain)
            {
                try
                {
                    await using (var reader = new JsonTextReader(File.OpenText(Resources.CacheFilePath)))
                    {
                        while (await reader.ReadAsync())
                        {
                            if (reader.TokenType != JsonToken.StartObject) { continue; }
                            var station = GetStationFromJson(reader);

                            if (station.Name.Contains(searchPhrase)) { stations.Add(station); }
                        }
                    }

                    tryAgain = false;
                }
                catch (IOException e)
                {
                    if (tries > 3) { throw new Exception("Something is wrong with reading the cache file.", e); }

                    tries++;
                    await Task.Delay(1000);
                }
            }

            return stations;
        }

        /// <summary>
        /// Retrieves a list of radio stations from a cached JSON file, sorted by the number of votes in descending order.
        /// </summary>
        /// <returns>A list of the top 100 most voted stations.</returns>
        public async Task<List<Station>> GetStationsByVotesAsync()
        {
            List<Station> stations = new();
            bool tryAgain = true;
            int tries = 0;

            while (tryAgain)
            {
                try
                {
                    await using (var reader = new JsonTextReader(File.OpenText(Resources.CacheFilePath)))
                    {
                        while (await reader.ReadAsync())
                        {
                            if (reader.TokenType != JsonToken.StartObject) { continue; }
                            var station = GetStationFromJson(reader);
                            stations.Add(station);
                        }
                    }

                    tryAgain = false;
                }
                catch (IOException e)
                {
                    if (tries > 3) { throw new Exception("Something is wrong with reading the cache file.", e); }

                    tries++;
                    await Task.Delay(1000);
                }
            }

            return stations.OrderByDescending(s => s.Votes).Take(100).ToList();
        }

        /// <summary>
        /// Gets a page of stations from the given list, specified by a starting and ending index (exclusive).
        /// </summary>
        /// <param name="from">The starting index of the page (inclusive).</param>
        /// <param name="to">The ending index of the page (exclusive).</param>
        /// <param name="stations">The list of stations to retrieve the page from.</param>
        /// <returns>A list of stations within the given page range.</returns>
        public async Task<List<Station>> GetPageOfStationsAsync(int from, int to, List<Station> stations)
        {
            List<Station> stationsInRange = new();
            await Task.Run(() =>
            {
                for (int i = from; i < to && i < stations.Count; i++)
                {
                    stationsInRange.Add(stations[i]);
                }
            });

            return stationsInRange;
        }

        /// <summary>
        /// Increase the click count of a station by one in the API.
        /// </summary>
        /// <param name="uuid">Station UUID</param>
        public async Task CountStationClickAsync(string uuid)
        {
            if (string.IsNullOrEmpty(uuid)) { return; }
            await new HttpClient().GetAsync($"http://{API.Url}/json/url/{uuid}");
        }

        /// <summary>
        /// Increase the click count of a station in the API and the local cache.
        /// </summary>
        /// <param name="uuid">The UUID of the station</param>
        public async Task VoteForStationAsync(string uuid)
        {
            if (string.IsNullOrEmpty(uuid)) { return; }
            if (DateTime.Now - LastVoteTime < TimeSpan.FromMinutes(10) && LastVoteUUIDs.Contains(uuid)) { return; }
            LastVoteTime = DateTime.Now;
            LastVoteUUIDs.Add(uuid);
            await new HttpClient().GetAsync($"http://{API.Url}/json/vote/{uuid}");

            // Since stations are cached locally, the vote count is updated manually so that the user can receive a visual response.
            await Task.Run(() =>
            {
                JObject? targetObject = null;
                JArray jsonArray;
                using (var fileStream = new FileStream(Resources.CacheFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var streamReader = new StreamReader(fileStream))
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    jsonArray = JArray.Load(jsonReader);
                    foreach (JObject obj in jsonArray.OfType<JObject>())
                    {
                        string fileUUID = obj["Item6"]?.ToString() ?? string.Empty;
                        if (uuid != fileUUID) continue;
                        targetObject = obj;
                        break;
                    }
                }

                if (targetObject != null)
                {
                    try
                    {
                        int newVoteCount = (int)targetObject["Item5"]! + 1;
                        targetObject["Item5"] = newVoteCount;
                    }
                    catch (Exception e) { throw new InvalidOperationException("Error updating vote count.", e); }

                    for (int i = 0; i < jsonArray.Count; i++)
                    {
                        if (jsonArray[i] == targetObject)
                        {
                            jsonArray[i] = targetObject;
                            break;
                        }
                    }

                    using var fileStreamWrite = new FileStream(Resources.CacheFilePath, FileMode.Create, FileAccess.Write);
                    using var streamWriter = new StreamWriter(fileStreamWrite);
                    using var jsonWriter = new JsonTextWriter(streamWriter);
                    jsonWriter.Formatting = Formatting.Indented;
                    jsonArray.WriteTo(jsonWriter);
                }

                Resources.EnforceClean();
            });
        }

        /// <summary>
        /// Retrieves all radio stations from an API and saves them to a JSON file.
        /// </summary>
        public async Task GetAllStations()
        {
            using HttpClient httpClient = new();
            using HttpResponseMessage response = await httpClient.GetAsync($"http://{API.Url}/json/stations?hidebroken=true&limit=500000");
            await using Stream stream = await response.Content.ReadAsStreamAsync();
            using StreamReader reader = new(stream);
            await using JsonTextReader jsonReader = new(reader);

            while (await jsonReader.ReadAsync())
            {
                if (jsonReader.TokenType == JsonToken.StartArray)
                {
                    await ParseAndSaveJArrayToJsonFileAsync(jsonReader);
                }
            }

            response.Dispose();
            httpClient.Dispose();
            await stream.DisposeAsync();
            reader.Dispose();
            jsonReader.Close();
            Resources.EnforceClean();
        }

        /// <summary>
        /// Parses a JsonReader object containing an array of station objects, trims and cleans up the station names,
        /// and saves the list of unique stations to a JSON file.
        /// </summary>
        /// <param name="reader">The JsonReader object containing the stations array.</param>
        private async Task ParseAndSaveJArrayToJsonFileAsync(JsonReader reader)
        {
            List<Station> stationList = new();

            while (await reader.ReadAsync())
            {
                if (reader.TokenType == JsonToken.StartObject)
                {
                    Station station = await ParseJsonObjectToStationAsync(reader);
                    if (!string.IsNullOrWhiteSpace(station.Name))
                    {
                        station.Name = station.Name.TrimStart('\t').Replace("\n", "").Replace("\t", "");
                        station.Name = Regex.Replace(station.Name, @"\s{3,}", " ").Trim();
                        stationList.Add(station);
                    }
                }
                else if (reader.TokenType == JsonToken.EndArray) { break; }
            }

            await SaveToJsonAsync(stationList.Distinct().ToList());
        }

        /// <summary>
        /// Parses a JsonReader object containing a single station object and returns a Station object.
        /// </summary>
        /// <param name="reader">The JsonReader object containing the station object to parse.</param>
        /// <returns>The parsed Station object.</returns>
        private async Task<Station> ParseJsonObjectToStationAsync(JsonReader reader)
        {
            Station station = new(string.Empty, string.Empty, string.Empty, string.Empty, 0, string.Empty);

            while (await reader.ReadAsync())
            {
                if (reader.TokenType == JsonToken.PropertyName)
                {
                    string propertyName = reader.Value?.ToString() ?? string.Empty;
                    await reader.ReadAsync();

                    switch (propertyName)
                    {
                        case "url_resolved":
                            station.Url = reader.Value?.ToString() ?? string.Empty;
                            break;
                        case "name":
                            station.Name = reader.Value?.ToString() ?? string.Empty;
                            break;
                        case "favicon":
                            station.IconUrl = reader.Value?.ToString() ?? string.Empty;
                            break;
                        case "countrycode":
                            station.CountryCode = reader.Value?.ToString() ?? string.Empty;
                            break;
                        case "votes":
                            station.Votes = int.Parse(reader.Value?.ToString() ?? string.Empty);
                            break;
                        case "stationuuid":
                            station.Uuid = reader.Value?.ToString() ?? string.Empty;
                            break;
                    }
                }
                else if (reader.TokenType == JsonToken.EndObject) { break; }
            }

            return station;
        }

        /// <summary>
        /// Saves a list of Station objects to a JSON file.
        /// </summary>
        /// <param name="list">The list of Station objects to be saved.</param>
        /// <exception cref="Exception">Thrown when an error occurs while saving to JSON.</exception>
        private async Task SaveToJsonAsync(List<Station> list)
        {
            await Task.Run(async () =>
            {
                bool tryAgain = true;
                int tries = 0;

                while (tryAgain)
                {
                    try
                    {
                        string json = JsonConvert.SerializeObject(list, Formatting.Indented);
                        await File.WriteAllTextAsync(Resources.CacheFilePath, json);
                        tryAgain = false;
                    }
                    catch (IOException e)
                    {
                        if (tries > 3) { throw new Exception("Failed to write to file after multiple attempts.", e); }

                        tries++;
                        await Task.Delay(1000);
                    }
                    catch (Exception e) { throw new Exception("An error occurred while saving to JSON.", e); }
                }
            });
        }

        /// <summary>
        /// Creates a new <see cref="Station"/> object from a JSON string.
        /// </summary>
        /// <param name="reader">The JSON reader.</param>
        /// <returns>A <see cref="Station"/> object.</returns>
        private static Station GetStationFromJson(JsonReader reader)
        {
            Station station = new(string.Empty, string.Empty, string.Empty, string.Empty, 0, string.Empty);

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.PropertyName)
                {
                    string propertyName = (string)reader.Value!;

                    switch (propertyName)
                    {
                        case "Url":
                            station = new(reader.ReadAsString() ?? string.Empty, station.Name, station.IconUrl, station.CountryCode, station.Votes, station.Uuid);
                            break;
                        case "Name":
                            station = new(station.Url, reader.ReadAsString() ?? string.Empty, station.IconUrl, station.CountryCode, station.Votes, station.Uuid);
                            break;
                        case "IconUrl":
                            station = new(station.Url, station.Name, reader.ReadAsString() ?? string.Empty, station.CountryCode, station.Votes, station.Uuid);
                            break;
                        case "CountryCode":
                            station = new(station.Url, station.Name, station.IconUrl, reader.ReadAsString() ?? string.Empty, station.Votes, station.Uuid);
                            break;
                        case "Votes":
                            station = new(station.Url, station.Name, station.IconUrl, station.CountryCode, reader.ReadAsInt32() ?? 0, station.Uuid);
                            break;
                        case "Uuid":
                            station = new(station.Url, station.Name, station.IconUrl, station.CountryCode, station.Votes, reader.ReadAsString() ?? string.Empty);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }
                else if (reader.TokenType == JsonToken.EndObject) { break; }
            }

            return station;
        }
    }
}
