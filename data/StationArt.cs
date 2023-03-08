using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Axios.data
{
    internal class StationArt
    {
        private static string TEMP_FOLDER_PATH = Path.GetTempPath() + "AxiosTemp";
        private string artURL = string.Empty;

        public StationArt(string artURL) { this.artURL = artURL; }

        public async Task<BitmapImage?> GetImageAsync()
        {
            if (artURL == string.Empty) { return null; }
            try
            {
                HttpClient client = new HttpClient();
                MemoryStream stream = new MemoryStream(await client.GetByteArrayAsync(artURL));
                if (!Directory.Exists(TEMP_FOLDER_PATH)) 
                { 
                    Directory.CreateDirectory(TEMP_FOLDER_PATH); 
                }
                string tempFilePath = Path.Combine(TEMP_FOLDER_PATH, "stationFavIcon_" + DateTime.Now.Ticks.ToString() + Path.GetExtension(artURL));
                using (FileStream fileStream = new FileStream(tempFilePath, FileMode.Create)) { stream.WriteTo(fileStream); }

                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(tempFilePath);
                image.EndInit();
                return image;
            }
            catch (Exception) { return null; }
        }

        public static void ClearTemp()
        {
            try
            {
                if (Directory.Exists(TEMP_FOLDER_PATH))
                {
                    foreach (string file in Directory.GetFiles(TEMP_FOLDER_PATH))
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception) { return; }
        }
    }
}
