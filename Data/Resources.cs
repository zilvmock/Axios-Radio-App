using System;
using System.IO;

namespace Axios.Data
{
    internal class Resources
    {
        public static readonly string TEMP_FOLDER_PATH = Path.GetTempPath() + "AxiosTemp";
        public static readonly string CACHE_FILE_PATH = Path.GetTempPath() + "AxiosTemp\\StationCache.json";

        public static void EnforceClean()
        {
            int collectionCount = 0;
            do
            {
                collectionCount++;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            } while (GC.GetTotalMemory(false) != 0 && collectionCount < 10);
        }

        public static void InitializeTempDir()
        {
            if (!Directory.Exists(TEMP_FOLDER_PATH))
            {
                Directory.CreateDirectory(TEMP_FOLDER_PATH);
            }
        }

        public static void ClearTempDir(bool deleteJsonCache = false)
        {
            try
            {
                if (Directory.Exists(TEMP_FOLDER_PATH))
                {
                    foreach (string file in Directory.GetFiles(TEMP_FOLDER_PATH))
                    {
                        if (Path.GetExtension(file) != ".json")
                        {
                            File.Delete(file);
                        }
                        else
                        {
                            if (deleteJsonCache) { File.Delete(file); }
                        }
                    }
                }
            }
            catch (Exception) { return; }
        }
    }
}
