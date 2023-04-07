using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Axios.Data;

namespace Axios.data
{
    internal class StationArt
    {
        private string artURL = string.Empty;

        public StationArt(string artURL) { this.artURL = artURL; }

        public async Task<BitmapImage?> GetImageAsync()
        {
            if (artURL == string.Empty) { return null; }
            try
            {
                HttpClient client = new HttpClient();
                MemoryStream stream = new MemoryStream(await client.GetByteArrayAsync(artURL));
                string tempFilePath = Path.Combine(Resources.TEMP_FOLDER_PATH, "stationFavIcon_" + DateTime.Now.Ticks.ToString() + Path.GetExtension(artURL));
                using (FileStream fileStream = new FileStream(tempFilePath, FileMode.Create)) { stream.WriteTo(fileStream); }

                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(tempFilePath);
                image.EndInit();
                return image;
            }
            catch (Exception) { return null; }
        }
    }
}
