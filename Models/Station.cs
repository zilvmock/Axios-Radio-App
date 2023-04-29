namespace Axios.Models
{
    public class Station
    {
        public string Url { get; set; }
        public string Name { get; set; }
        public string IconUrl { get; set; }
        public string CountryCode { get; set; }
        public int Votes { get; set; }
        public string Uuid { get; set; }

        public Station(string url, string name, string iconUrl, string countryCode, int votes, string uuid)
        {
            Url = url;
            Name = name;
            IconUrl = iconUrl;
            CountryCode = countryCode;
            Votes = votes;
            Uuid = uuid;
        }
    }
}
