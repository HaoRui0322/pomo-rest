using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace PomoRest.Core
{
    public static class CsvExporter
    {
        public static void WriteCsv(string path, string[] headers, System.Collections.Generic.IEnumerable<string[]> rows)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var sw = new StreamWriter(fs, new UTF8Encoding(true)))
            {
                WriteRow(sw, headers);
                foreach (string[] row in rows)
                    WriteRow(sw, row);
            }
        }

        private static void WriteRow(StreamWriter sw, string[] cells)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0) sb.Append(',');
                string cell = cells[i] ?? string.Empty;
                bool needsQuote = cell.IndexOf('"') >= 0 || cell.IndexOf(',') >= 0 ||
                                  cell.IndexOf('\n') >= 0 || cell.IndexOf('\r') >= 0;
                if (needsQuote)
                {
                    sb.Append('"');
                    sb.Append(cell.Replace("\"", "\"\""));
                    sb.Append('"');
                }
                else
                {
                    sb.Append(cell);
                }
            }
            sw.Write(sb.ToString());
            sw.Write("\r\n");
        }

        public static string FileStamp(DateTime now)
        {
            return now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        }

        public static void ExportDetail(HistoryData history, string path)
        {
            string[] headers = new string[] { "日期", "开始时间", "结束时间", "计划时长(分钟)", "实际时长(分钟)", "状态", "备注" };
            var rows = new System.Collections.Generic.List<string[]>();
            foreach (DayRecord day in history.DaysDescending())
            {
                if (day == null || day.Sessions == null) continue;
                foreach (FocusSessionRecord s in day.Sessions)
                {
                    if (s == null) continue;
                    DateTime start = HistoryData.FromIsoUtc(s.StartUtc, DateTime.Now);
                    DateTime end = HistoryData.FromIsoUtc(s.EndUtc, DateTime.Now);
                    rows.Add(new string[]
                    {
                        day.Date,
                        start.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                        end.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                        Round2(s.PlannedSeconds).ToString(CultureInfo.InvariantCulture),
                        Round2(s.ActualSeconds).ToString(CultureInfo.InvariantCulture),
                        TimeFormat.StatusLabel(s.Status),
                        s.Note ?? string.Empty
                    });
                }
            }
            WriteCsv(path, headers, rows);
        }

        public static void ExportSummary(HistoryData history, string path)
        {
            string[] headers = new string[] { "日期", "完成番茄数", "专注分钟", "护眼次数", "备注数" };
            var rows = new System.Collections.Generic.List<string[]>();
            foreach (DayRecord day in history.DaysDescending())
            {
                if (day == null) continue;
                rows.Add(new string[]
                {
                    day.Date,
                    day.FocusCompleted.ToString(CultureInfo.InvariantCulture),
                    ((int)Math.Round(day.FocusSeconds / 60.0)).ToString(CultureInfo.InvariantCulture),
                    day.EyeRests.ToString(CultureInfo.InvariantCulture),
                    day.NoteCount.ToString(CultureInfo.InvariantCulture)
                });
            }
            WriteCsv(path, headers, rows);
        }

        private static double Round2(int seconds)
        {
            return Math.Round(seconds / 60.0, 2);
        }
    }
}
