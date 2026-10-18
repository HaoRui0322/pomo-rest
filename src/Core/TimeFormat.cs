using System;
using System.Globalization;

namespace PomoRest.Core
{
    public static class TimeFormat
    {
        public static string MmSs(int seconds)
        {
            if (seconds < 0) seconds = 0;
            int m = seconds / 60;
            int s = seconds % 60;
            return m.ToString("00", CultureInfo.InvariantCulture) + ":" + s.ToString("00", CultureInfo.InvariantCulture);
        }

        public static string DurationText(int seconds)
        {
            if (seconds < 60) return seconds + " 秒";
            int m = (int)Math.Round(seconds / 60.0);
            if (m < 60) return m + " 分钟";
            int h = m / 60;
            int rem = m % 60;
            if (rem == 0) return h + " 小时";
            return h + " 小时 " + rem + " 分";
        }

        public static string ClockTime(DateTime local)
        {
            return local.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }

        public static string DayLabel(string dateKey)
        {
            DateTime d;
            if (!DateTime.TryParseExact(dateKey, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                return dateKey;
            if (d.Date == DateTime.Today) return "今天 · " + d.ToString("M月d日", CultureInfo.GetCultureInfo("zh-CN"));
            string week = d.ToString("ddd", CultureInfo.GetCultureInfo("zh-CN"));
            return d.ToString("M月d日 ", CultureInfo.GetCultureInfo("zh-CN")) + week;
        }

        public static string ShortDayLabel(string dateKey)
        {
            DateTime d;
            if (!DateTime.TryParseExact(dateKey, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                return dateKey;
            return d.ToString("yyyy/M/d", CultureInfo.InvariantCulture);
        }

        public static string PhaseLabel(TimerPhase phase)
        {
            switch (phase)
            {
                case TimerPhase.Focus: return "专注";
                case TimerPhase.ShortBreak: return "短休息";
                case TimerPhase.LongBreak: return "长休息";
                case TimerPhase.EyeWork: return "屏幕使用";
                case TimerPhase.EyeRest: return "远眺放松";
                default: return "—";
            }
        }

        public static string StatusLabel(string status)
        {
            switch (status)
            {
                case FocusStatus.Completed: return "完成";
                case FocusStatus.Skipped: return "跳过";
                case FocusStatus.Reset: return "重置";
                case FocusStatus.Interrupted: return "中断";
                default: return status;
            }
        }
    }
}
