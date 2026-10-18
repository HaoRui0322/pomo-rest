using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using PomoRest.Core;

namespace PomoRest.Services
{
    public sealed class StateStore
    {
        private readonly string _path;

        public StateStore(string path)
        {
            _path = path;
        }

        public AppState Load()
        {
            EnsureDir();
            if (!File.Exists(_path)) return AppState.CreateDefault();

            try
            {
                string json = File.ReadAllText(_path, Encoding.UTF8);
                var state = Deserialize(json);
                if (state == null) return AppState.CreateDefault();
                if (state.Settings == null) state.Settings = AppSettings.CreateDefaults();
                if (state.Window == null) state.Window = new WindowStateData();
                if (state.Session == null) state.Session = new SessionStateData();
                state.Settings.Normalize();
                return state;
            }
            catch
            {
                BackupCorrupt();
                return AppState.CreateDefault();
            }
        }

        public bool Save(AppState state)
        {
            EnsureDir();
            try
            {
                state.Version = 1;
                if (state.Settings == null) state.Settings = AppSettings.CreateDefaults();
                if (state.Window == null) state.Window = new WindowStateData();
                if (state.Session == null) state.Session = new SessionStateData();
                state.Settings.Normalize();

                string json = Serialize(state);
                string tmp = _path + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false));
                if (File.Exists(_path))
                {
                    File.Replace(tmp, _path, null);
                }
                else
                {
                    File.Move(tmp, _path);
                }
                return true;
            }
            catch
            {
                try { File.Delete(_path + ".tmp"); } catch { }
                return false;
            }
        }

        private void EnsureDir()
        {
            try
            {
                string dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            }
            catch { }
        }

        private static string Serialize(AppState state)
        {
            var serializer = new DataContractJsonSerializer(typeof(AppState));
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, state);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private static AppState Deserialize(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(AppState));
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            using (var ms = new MemoryStream(bytes))
            {
                return (AppState)serializer.ReadObject(ms);
            }
        }

        private void BackupCorrupt()
        {
            try
            {
                string stamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                string bak = _path + ".corrupt-" + stamp;
                if (File.Exists(_path)) File.Move(_path, bak);
            }
            catch { }
        }
    }
}
