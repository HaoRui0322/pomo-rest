using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;

namespace PomoRest.Core
{
    public static class FocusStatus
    {
        public const string Completed = "completed";
        public const string Skipped = "skipped";
        public const string Reset = "reset";
        public const string Interrupted = "interrupted";
    }

    [DataContract]
    public sealed class FocusSessionRecord
    {
        [DataMember(Name = "startUtc")]
        public string StartUtc;
        [DataMember(Name = "endUtc")]
        public string EndUtc;
        [DataMember(Name = "plannedSeconds")]
        public int PlannedSeconds;
        [DataMember(Name = "actualSeconds")]
        public int ActualSeconds;
        [DataMember(Name = "status")]
        public string Status;
        [DataMember(Name = "note")]
        public string Note;

        public FocusSessionRecord()
        {
            Note = string.Empty;
        }
    }

    [DataContract]
    public sealed class DayRecord
    {
        [DataMember(Name = "date")]
        public string Date;
        [DataMember(Name = "eyeRests")]
        public int EyeRests;
        [DataMember(Name = "sessions")]
        public List<FocusSessionRecord> Sessions;

        public DayRecord()
        {
            Sessions = new List<FocusSessionRecord>();
        }

        public int FocusCompleted
        {
            get
            {
                int n = 0;
                foreach (FocusSessionRecord s in Sessions)
                    if (s != null && s.Status == FocusStatus.Completed) n++;
                return n;
            }
        }

        public int FocusSeconds
        {
            get
            {
                int n = 0;
                foreach (FocusSessionRecord s in Sessions)
                    if (s != null && s.Status == FocusStatus.Completed) n += s.ActualSeconds;
                return n;
            }
        }

        public int FocusMinutes
        {
            get { return (int)Math.Round(FocusSeconds / 60.0); }
        }

        public int NoteCount
        {
            get
            {
                int n = 0;
                foreach (FocusSessionRecord s in Sessions)
                    if (s != null && !string.IsNullOrEmpty(s.Note)) n++;
                return n;
            }
        }
    }

    [DataContract]
    public sealed class HistoryData
    {
        [DataMember(Name = "version")]
        public int Version = 1;

        [DataMember(Name = "days")]
        public List<DayRecord> Days = new List<DayRecord>();

        public DayRecord GetOrCreateDay(string date)
        {
            foreach (DayRecord d in Days)
                if (d != null && d.Date == date) return d;
            var day = new DayRecord { Date = date };
            Days.Add(day);
            return day;
        }

        public DayRecord FindDay(string date)
        {
            foreach (DayRecord d in Days)
                if (d != null && d.Date == date) return d;
            return null;
        }

        public List<DayRecord> DaysDescending()
        {
            var list = new List<DayRecord>();
            foreach (DayRecord d in Days)
                if (d != null) list.Add(d);
            list.Sort(delegate(DayRecord a, DayRecord b)
            {
                return string.CompareOrdinal(b.Date, a.Date);
            });
            return list;
        }

        public bool IsEmpty()
        {
            foreach (DayRecord d in Days)
            {
                if (d == null) continue;
                if (d.EyeRests > 0 || (d.Sessions != null && d.Sessions.Count > 0)) return false;
            }
            return true;
        }

        public static string DateKey(DateTime local)
        {
            return local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static string ToIsoUtc(DateTime local)
        {
            return local.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }

        public static DateTime FromIsoUtc(string iso, DateTime fallbackLocal)
        {
            DateTime dt;
            if (!string.IsNullOrEmpty(iso) && DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out dt))
                return dt.ToLocalTime();
            return fallbackLocal;
        }
    }

    public sealed class TodaySummary
    {
        public int FocusCompleted;
        public int FocusSeconds;
        public int EyeRests;

        public int FocusMinutes
        {
            get { return (int)Math.Round(FocusSeconds / 60.0); }
        }

        public static TodaySummary FromDay(DayRecord day)
        {
            var s = new TodaySummary();
            if (day != null)
            {
                s.FocusCompleted = day.FocusCompleted;
                s.FocusSeconds = day.FocusSeconds;
                s.EyeRests = day.EyeRests;
            }
            return s;
        }
    }

    public sealed class AllTimeSummary
    {
        public int FocusCompleted;
        public int FocusSeconds;
        public int EyeRests;
        public int DaysWithData;

        public int FocusMinutes
        {
            get { return (int)Math.Round(FocusSeconds / 60.0); }
        }

        public static AllTimeSummary FromHistory(HistoryData h)
        {
            var s = new AllTimeSummary();
            if (h == null) return s;
            foreach (DayRecord d in h.DaysDescending())
            {
                if (d == null) continue;
                s.FocusCompleted += d.FocusCompleted;
                s.FocusSeconds += d.FocusSeconds;
                s.EyeRests += d.EyeRests;
                s.DaysWithData++;
            }
            return s;
        }
    }
}
