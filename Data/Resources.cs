using System;
using System.IO;

namespace Axios.Data
{
    /// <summary>
    /// A class for managing application resources such as garbage collector and cache.
    /// </summary>
    internal class Resources
    {
        public static readonly string TempFolderPath = Path.GetTempPath() + "AxiosTemp";
        public static readonly string CacheFilePath = Path.GetTempPath() + "AxiosTemp\\StationCache.json";

        /// <summary>
        /// Forces garbage collection and waits for finalizers to finish, repeatedly collecting until no memory is left.
        /// </summary>
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

        /// <summary>
        /// Initializes the temporary directory if it does not exist.
        /// </summary>
        public static void InitializeTempDir()
        {
            if (!Directory.Exists(TempFolderPath)) { Directory.CreateDirectory(TempFolderPath); }
        }

        /// <summary>
        /// Clears the temporary directory by deleting all files.
        /// </summary>
        /// <param name="deleteJsonCache">Whether or not to delete the JSON cache file.</param>
        /// <exception cref="Exception">Thrown when failed to clear the temp directory.</exception>
        public static void ClearTempDir(bool deleteJsonCache = false)
        {
            try
            {
                if (!Directory.Exists(TempFolderPath)) { return; }

                foreach (string file in Directory.GetFiles(TempFolderPath))
                {
                    if (Path.GetExtension(file) != ".json")
                    {
                        try { File.Delete(file); }
                        catch (IOException ex) { continue; }
                        catch (UnauthorizedAccessException ex) { continue; }
                    }
                    else { if (deleteJsonCache) { File.Delete(file); } }
                }
            }
            catch (Exception e) { throw new Exception("Failed to clear temp directory.", e); }
        }
    }
}
