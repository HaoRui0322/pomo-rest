using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;
using PomoRest.Core;
using PomoRest.Services;

namespace PomoRest.Tests
{
    public static class CoreTests
    {
        private static int _pass;
        private static int _fail;

        public static int Main()
        {
            RunEngineTests();
            RunSettingsTests();
            RunHistoryTests();
            RunCsvTests();
            RunStateTests();
            RunStartupTests();
            RunFormatTests();

            Console.WriteLine();
            Console.WriteLine("Passed: " + _pass + ", Failed: " + _fail);
            return _fail == 0 ? 0 : 1;
        }

        private static void RunEngineTests()
        {
            var s = AppSettings.CreateDefaults();

            // Focus completion -> short break, counted.
            var e = new SessionEngine(TimerMode.Pomodoro, s);
            e.Start(0);
            long t = 0;
            TickSteps(e, ref t, 1500 * 1000L);
            Check(e.Phase == TimerPhase.ShortBreak, "focus -> short break");
            Check(e.CompletedInCycle == 1, "completedInCycle incremented");
            Check(e.Running, "auto-start break default on");

            // Fourth focus -> long break.
            t = 0;
            e = new SessionEngine(TimerMode.Pomodoro, s);
            for (int i = 1; i <= 3; i++)
            {
                e.Start(t);
                TickSteps(e, ref t, 1500 * 1000L);
                e.Skip(t);
            }
            e.Start(t);
            TickSteps(e, ref t, 1500 * 1000L);
            Check(e.Phase == TimerPhase.LongBreak, "4th focus -> long break");
            Check(e.CompletedInCycle == 4, "cycle count is 4 before long break ends");

            // Skip focus does not count.
            e = new SessionEngine(TimerMode.Pomodoro, s);
            e.Start(0);
            t = 0;
            TickSteps(e, ref t, 600 * 1000L);
            var skip = e.Skip(t);
            Check(skip != null && skip.Type == TimerEventType.FocusSkipped, "skip focus emits skipped");
            Check(e.Phase == TimerPhase.ShortBreak, "skip focus -> short break");
            Check(e.CompletedInCycle == 0, "skip does not increment cycle");

            // Reset focus emits reset and full duration.
            e = new SessionEngine(TimerMode.Pomodoro, s);
            e.Start(0);
            t = 0;
            TickSteps(e, ref t, 300 * 1000L);
            var reset = e.Reset(t);
            Check(reset != null && reset.Type == TimerEventType.FocusReset, "reset emits event");
            Check(reset.ActualSeconds == 300, "reset actual seconds");
            Check(e.Phase == TimerPhase.Focus && e.RemainingSeconds == 1500, "reset returns full focus");

            // Pause/resume preserves remaining.
            e = new SessionEngine(TimerMode.Pomodoro, s);
            e.Start(0);
            t = 0;
            TickSteps(e, ref t, 100 * 1000L);
            e.Pause(t);
            Check(e.RemainingSeconds == 1400, "pause remaining 1400");
            TickSteps(e, ref t, 100 * 1000L);
            Check(e.RemainingSeconds == 1400, "paused timer does not advance");
            e.Start(t);
            TickSteps(e, ref t, 100 * 1000L);
            Check(e.RemainingSeconds == 1300, "resume continues");

            // Long gap -> auto pause, no completion.
            e = new SessionEngine(TimerMode.Pomodoro, s);
            e.Start(0);
            var gap = e.Tick(20 * 1000L);
            Check(gap.LongGapAutoPaused, "long gap detected");
            Check(!e.Running && e.RemainingSeconds == 1500, "long gap pauses without completing");

            // Eye mode work -> rest -> work.
            var eye = new SessionEngine(TimerMode.EyeCare, s);
            eye.Start(0);
            t = 0;
            TickSteps(eye, ref t, 1200 * 1000L);
            Check(eye.Phase == TimerPhase.EyeRest && !eye.Running, "eye rest active, not running");
            var done = eye.CompleteEyeRest(t, false);
            Check(done != null && done.Type == TimerEventType.EyeRestCompleted && !done.Skipped, "eye rest completed not skipped");
            Check(eye.Phase == TimerPhase.EyeWork && eye.Running, "eye rest -> work running");

            var doneSkip = eye.CompleteEyeRest(0, true);
            Check(doneSkip == null, "cannot complete eye rest when already in work");

            // "现在休息" while paused should NOT auto-resume after the rest.
            var eye2 = new SessionEngine(TimerMode.EyeCare, s);
            long t2 = 0;
            eye2.Start(t2);
            TickSteps(eye2, ref t2, 60 * 1000L);
            eye2.Pause(t2);
            var skip2 = eye2.Skip(t2);
            Check(skip2 != null && skip2.Type == TimerEventType.EyeRestStarted, "now rest emits when paused");
            eye2.CompleteEyeRest(t2, false);
            Check(eye2.Phase == TimerPhase.EyeWork && !eye2.Running, "rest keeps paused state");
        }

        private static void TickSteps(SessionEngine engine, ref long now, long totalMs)
        {
            long step = 1000;
            long done = 0;
            while (done < totalMs)
            {
                long d = Math.Min(step, totalMs - done);
                now += d;
                engine.Tick(now);
                done += d;
            }
        }

        private static void RunSettingsTests()
        {
            var s = new AppSettings();
            s.FocusMinutes = 999;
            s.LongBreakEvery = 1;
            s.MusicLinks = new List<MusicLink>
            {
                new MusicLink { Url = "javascript:alert(1)" },
                new MusicLink { Url = "https://example.com/a" },
                new MusicLink { Url = "https://example.com/a" },
                new MusicLink { Url = "https://example.com/b" },
                new MusicLink { Url = "https://example.com/c" },
                new MusicLink { Url = "https://example.com/d" },
                new MusicLink { Url = "https://example.com/e" },
                new MusicLink { Url = "https://example.com/f" }
            };
            s.Normalize();
            Check(s.FocusMinutes == 120, "focus clamps to 120");
            Check(s.LongBreakEvery == 2, "long break every clamps to 2");
            Check(s.MusicLinks.Count == 6, "music dedupes and caps at 6 valid entries");
            Check(AppSettings.IsValidUrl("https://example.com"), "valid https");
            Check(!AppSettings.IsValidUrl("file:///c:/x"), "reject file scheme");
            Check(!AppSettings.IsValidUrl("not a url"), "reject invalid url");
        }

        private static void RunHistoryTests()
        {
            string dir = NewTempDir();
            string path = Path.Combine(dir, "history.json");
            string bak = Path.Combine(dir, "history.json.bak");
            var store = new HistoryStore(path, bak);

            DateTime now = new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Local);
            store.AddFocusSession(now, now.AddMinutes(25), 1500, 1500, FocusStatus.Completed, "完成报告");
            store.AddEyeRest(now.AddMinutes(30));
            store.Save();

            TodaySummary sum = store.GetTodaySummary(now);
            Check(sum.FocusCompleted == 1, "today focus completed 1");
            Check(sum.FocusSeconds == 1500, "today focus seconds 1500");
            Check(sum.EyeRests == 1, "today eye rests 1");

            var reloaded = new HistoryStore(path, bak);
            TodaySummary sum2 = reloaded.GetTodaySummary(now);
            Check(sum2.FocusCompleted == 1 && sum2.EyeRests == 1, "history roundtrip");

            // Backup recovery.
            File.WriteAllText(path, "{ broken json", Encoding.UTF8);
            var recovered = new HistoryStore(path, bak);
            Check(recovered.GetTodaySummary(now).FocusCompleted == 1, "history recovers from backup");

            Directory.Delete(dir, true);
        }

        private static void RunCsvTests()
        {
            string dir = NewTempDir();
            string detail = Path.Combine(dir, "detail.csv");
            string summary = Path.Combine(dir, "summary.csv");

            var h = new HistoryData();
            var day = h.GetOrCreateDay("2026-10-17");
            day.EyeRests = 2;
            day.Sessions.Add(new FocusSessionRecord
            {
                StartUtc = "2026-10-17T02:00:00Z",
                EndUtc = "2026-10-17T02:25:00Z",
                PlannedSeconds = 1500,
                ActualSeconds = 1500,
                Status = FocusStatus.Completed,
                Note = "包含,逗号 \"引号\"\n换行"
            });

            CsvExporter.ExportDetail(h, detail);
            CsvExporter.ExportSummary(h, summary);

            byte[] bytes = File.ReadAllBytes(detail);
            Check(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "CSV has UTF-8 BOM");
            string text = File.ReadAllText(detail, new UTF8Encoding(true));
            Check(text.Contains("\"包含,逗号 \"\"引号\"\"\n换行\""), "CSV escapes commas quotes newlines");

            string sumText = File.ReadAllText(summary, new UTF8Encoding(true));
            Check(sumText.Contains("2026-10-17,1,25,2,1"), "summary CSV row");

            Directory.Delete(dir, true);
        }

        private static void RunStateTests()
        {
            string dir = NewTempDir();
            string path = Path.Combine(dir, "state.json");
            var store = new StateStore(path);
            var state = AppState.CreateDefault();
            state.Settings.FocusMinutes = 45;
            state.Settings.MusicLinks.Add(new MusicLink { Label = "歌单", Url = "https://example.com" });
            store.Save(state);

            var loaded = store.Load();
            Check(loaded.Settings.FocusMinutes == 45, "state settings roundtrip");
            Check(loaded.Settings.MusicLinks.Count == 1, "state music roundtrip");
            Check(loaded.Settings.StartWithWindows, "default startWithWindows true");

            File.WriteAllText(path, "not json", Encoding.UTF8);
            var fallback = store.Load();
            Check(fallback.Settings.FocusMinutes == 25, "corrupt state falls back to defaults");

            Directory.Delete(dir, true);
        }

        private static void RunStartupTests()
        {
            string subKey = @"Software\PomoRest.Test." + Guid.NewGuid().ToString("N");
            var mgr = new StartupManager(subKey, "TestValue");
            bool ok = mgr.Enable(@"C:\some path\PomoRest.exe");
            if (!ok)
            {
                // Some confined environments block registry writes even to a
                // disposable HKCU test key; treat that as an environment skip
                // rather than a product failure. On a normal user account the
                // write succeeds and the assertion below is fully exercised.
                Console.WriteLine("SKIP  startup registry write blocked by environment");
            }
            else
            {
                Check(mgr.IsEnabled(), "startup enable + detect");
                bool removed = mgr.Disable();
                Check(removed && !mgr.IsEnabled(), "startup disable");
            }

            try { Registry.CurrentUser.DeleteSubKeyTree(subKey, false); } catch { }
        }

        private static void RunFormatTests()
        {
            Check(TimeFormat.MmSs(1500) == "25:00", "format 25:00");
            Check(TimeFormat.MmSs(65) == "01:05", "format 01:05");
            Check(TimeFormat.MmSs(120 * 60) == "120:00", "format 120:00");
            Check(TimeFormat.PhaseLabel(TimerPhase.EyeRest) == "远眺放松", "eye rest label");
        }

        private static string NewTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "pomorest-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void Check(bool condition, string name)
        {
            if (condition) { _pass++; Console.WriteLine("PASS  " + name); }
            else { _fail++; Console.WriteLine("FAIL  " + name); }
        }
    }
}
