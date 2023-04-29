using System.Net.NetworkInformation;
using System.Net;

namespace Axios.data
{
    /// <summary>
    /// Class for getting the working API URL for radio stations.
    /// </summary>
    internal class API
    {
        private static string _apiUrl;

        public static string Url
        {
            get
            {
                if (string.IsNullOrEmpty(_apiUrl)) { _apiUrl = GetWorkingApiUrl(); }
                return _apiUrl;
            }
        }

        /// <summary>
        /// Gets the working API URL for radio stations by pinging available IPs and returning the IP with the lowest round-trip time.
        /// </summary>
        /// <returns>The working API URL for radio stations.</returns>
        private static string GetWorkingApiUrl()
        {
            string baseUrl = @"all.api.radio-browser.info";
            var ips = Dns.GetHostAddresses(baseUrl);
            long lastRoundTripTime = long.MaxValue;
            string searchUrl = @"de1.api.radio-browser.info";
            foreach (IPAddress ipAddress in ips)
            {
                var reply = new Ping().Send(ipAddress);
                if (reply != null &&
                    reply.RoundtripTime < lastRoundTripTime)
                {
                    lastRoundTripTime = reply.RoundtripTime;
                    searchUrl = ipAddress.ToString();
                }
            }

            IPHostEntry hostEntry = Dns.GetHostEntry(searchUrl);
            if (!string.IsNullOrEmpty(hostEntry.HostName))
            {
                searchUrl = hostEntry.HostName;
            }

            return searchUrl;
        }
    }
}
