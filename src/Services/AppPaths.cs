using System;
using System.IO;

namespace PomoRest.Services
{
    public static class AppPaths
    {
        public static string DataDirectory
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "PomoRest");
            }
        }

        public static string StateFile { get { return Path.Combine(DataDirectory, "state.json"); } }
        public static string HistoryFile { get { return Path.Combine(DataDirectory, "history.json"); } }
        public static string HistoryBackupFile { get { return Path.Combine(DataDirectory, "history.json.bak"); } }
        public static string ErrorLogFile { get { return Path.Combine(DataDirectory, "error.log"); } }

        public static void EnsureDirectory()
        {
            Directory.CreateDirectory(DataDirectory);
        }
    }
}
