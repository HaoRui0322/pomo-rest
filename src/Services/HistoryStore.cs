using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using PomoRest.Core;

namespace PomoRest.Services
{
    public sealed class HistoryStore
    {
        private readonly string _path;
        private readonly string _backupPath;
        private HistoryData _data;
        private bool _dirty;

        public HistoryStore(string path, string backupPath)
        {
            _path = path;
            _backupPath = backupPath;
            _data = Load();
        }

        public HistoryData Data { get { return _data; } }

        public HistoryData Load()
        {
            EnsureDir();

            HistoryData fromMain = TryLoad(_path);
            if (fromMain != null) return fromMain;

            HistoryData fromBak = TryLoad(_backupPath);
            if (fromBak != null)
            {
                // Keep a copy of the recovered data as the new main file.
                _data = fromBak;
                _dirty = true;
                Save();
                return fromBak;
            }

            return new HistoryData();
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

        private HistoryData TryLoad(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                var serializer = new DataContractJsonSerializer(typeof(HistoryData));
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    HistoryData h = (HistoryData)serializer.ReadObject(ms);
                    if (h == null) return null;
                    if (h.Days == null) h.Days = new System.Collections.Generic.List<DayRecord>();
                    return h;
                }
            }
            catch
            {
                return null;
            }
        }

        public bool Save()
        {
            EnsureDir();
            try
            {
                string json = Serialize(_data);
                string tmp = _path + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false));
                if (File.Exists(_path))
                {
                    // Rotate the current main file into the backup slot first.
                    try { File.Copy(_path, _backupPath, true); } catch { }
                    File.Replace(tmp, _path, null);
                }
                else
                {
                    File.Move(tmp, _path);
                    // Keep a backup copy even after the very first write so that a
                    // later corruption of the main file can still be recovered.
                    try { File.Copy(_path, _backupPath, true); } catch { }
                }
                _dirty = false;
                return true;
            }
            catch
            {
                try { File.Delete(_path + ".tmp"); } catch { }
                return false;
            }
        }

        public void MarkDirty()
        {
            _dirty = true;
        }

        public bool IsDirty { get { return _dirty; } }

        public void AddFocusSession(DateTime startLocal, DateTime endLocal, int plannedSeconds, int actualSeconds, string status, string note)
        {
            string dateKey = HistoryData.DateKey(endLocal);
            DayRecord day = _data.GetOrCreateDay(dateKey);
            var rec = new FocusSessionRecord
            {
                StartUtc = HistoryData.ToIsoUtc(startLocal),
                EndUtc = HistoryData.ToIsoUtc(endLocal),
                PlannedSeconds = plannedSeconds,
                ActualSeconds = actualSeconds,
                Status = status,
                Note = (note ?? string.Empty).Trim()
            };
            day.Sessions.Add(rec);
            _dirty = true;
        }

        public void AddEyeRest(DateTime localNow)
        {
            string dateKey = HistoryData.DateKey(localNow);
            DayRecord day = _data.GetOrCreateDay(dateKey);
            day.EyeRests++;
            _dirty = true;
        }

        public TodaySummary GetTodaySummary(DateTime now)
        {
            return TodaySummary.FromDay(_data.FindDay(HistoryData.DateKey(now)));
        }

        public AllTimeSummary GetAllTimeSummary()
        {
            return AllTimeSummary.FromHistory(_data);
        }

        private static string Serialize(HistoryData data)
        {
            var serializer = new DataContractJsonSerializer(typeof(HistoryData));
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, data);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
    }
}
