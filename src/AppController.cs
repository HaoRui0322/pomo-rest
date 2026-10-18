using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using PomoRest.Core;
using PomoRest.Services;
using PomoRest.UI;

namespace PomoRest
{
    public sealed class AppController
    {
        private readonly Application _app;
        private readonly StateStore _stateStore;
        private readonly HistoryStore _historyStore;
        private readonly StartupManager _startupManager;
        private readonly MusicLauncher _musicLauncher;
        private readonly ChimePlayer _chime;

        private AppState _state;
        private AppSettings _settings;

        private SessionEngine _pomodoroEngine;
        private SessionEngine _eyeEngine;
        private SessionEngine _activeEngine;

        private MainWindow _window;
        private BreakWindow _breakWindow;
        private TrayController _tray;

        private DispatcherTimer _tickTimer;
        private DispatcherTimer _topmostTimer;
        private DispatcherTimer _saveTimer;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        private DateTime? _attemptStartLocal;
        private int _attemptPlannedSeconds;
        private string _noteDraft = string.Empty;
        private bool _eyeRestActive;
        private bool _resumeAfterSystemPause;
        private bool _shuttingDown;
        private bool _hintShown;
        private string _lastDateKey;
        private string _lastMusicSignature = string.Empty;

        public AppController(Application app)
        {
            _app = app;
            _stateStore = new StateStore(AppPaths.StateFile);
            _historyStore = new HistoryStore(AppPaths.HistoryFile, AppPaths.HistoryBackupFile);
            _startupManager = new StartupManager();
            _musicLauncher = new MusicLauncher();
            _chime = new ChimePlayer();
        }

        public void Start()
        {
            UiResources.MergeTheme();

            _state = _stateStore.Load();
            _settings = _state.Settings.Clone();
            _settings.Normalize();

            _pomodoroEngine = new SessionEngine(TimerMode.Pomodoro, _settings);
            _eyeEngine = new SessionEngine(TimerMode.EyeCare, _settings);

            RestoreSession();

            _window = new MainWindow();
            _breakWindow = new BreakWindow();
            _tray = new TrayController();

            WireEvents();

            _lastDateKey = HistoryData.DateKey(DateTime.Now);
            _lastMusicSignature = MusicSignature();

            _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _tickTimer.Tick += OnTick;
            _tickTimer.Start();

            _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
            _topmostTimer.Tick += delegate
            {
                if (_settings.AlwaysOnTop) _window.EnforceTopmost();
                if (_breakWindow != null && _breakWindow.IsVisible) _breakWindow.EnforceTopmost();
            };
            _topmostTimer.Start();

            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            _saveTimer.Tick += delegate { SaveState(); _saveTimer.Stop(); };

            SubscribeSystemEvents();

            _window.ApplyWindowBounds(_state.Window.Left, _state.Window.Top, _state.Window.Compact);
            _tray.SetCompact(_state.Window.Compact);
            _window.SetAlwaysOnTop(_settings.AlwaysOnTop);
            _tray.SetTopmost(_settings.AlwaysOnTop);

            EnsureStartupState();

            _window.Show();
            _window.BringToFront();
            _tray.SetVisible(true);
            _tray.SetMode(_activeEngine.Mode);

            RenderUi();
        }

        private void RestoreSession()
        {
            SessionStateData s = _state.Session;
            if (s == null) s = new SessionStateData();

            TimerMode mode = ParseMode(s.Mode);
            _activeEngine = mode == TimerMode.Pomodoro ? _pomodoroEngine : _eyeEngine;

            TimerPhase phase = ParsePhase(s.Phase);
            if (_activeEngine.Mode == TimerMode.Pomodoro)
            {
                if (phase != TimerPhase.Focus && phase != TimerPhase.ShortBreak && phase != TimerPhase.LongBreak)
                    phase = TimerPhase.Focus;
            }
            else
            {
                if (phase != TimerPhase.EyeWork && phase != TimerPhase.EyeRest)
                    phase = TimerPhase.EyeWork;
                if (phase == TimerPhase.EyeRest) phase = TimerPhase.EyeWork;
            }

            int remaining = s.RemainingSeconds;
            if (remaining <= 0) remaining = _activeEngine.PhaseDurationSeconds(phase);
            _activeEngine.RestoreState(phase, remaining, s.CompletedInCycle, NowMs());

            _noteDraft = s.NoteDraft ?? string.Empty;
            // A crashed/interrupted attempt is intentionally not auto-finalized on
            // startup; the note draft stays available for the next focus session.
        }

        private void WireEvents()
        {
            _window.StartPauseRequested += delegate { HandleStartPause(); };
            _window.ResetRequested += delegate { HandleReset(); };
            _window.SkipRequested += delegate { HandleSkip(); };
            _window.HideRequested += delegate { HandleHide(); };
            _window.ExitRequested += delegate { Shutdown(); };
            _window.ExportDetailRequested += delegate { ExportDetail(); };
            _window.ExportSummaryRequested += delegate { ExportSummary(); };
            _window.ResetDefaultsRequested += delegate { HandleResetDefaults(); };
            _window.ShowSignalReceived += delegate { _window.BringToFront(); };
            _window.TaskbarCreated += delegate { if (_tray != null) _tray.Refresh(); };
            _window.CompactToggleRequested += delegate { ToggleCompact(); };
            _window.TopmostToggleRequested += delegate { ToggleTopmost(); };
            _window.HistoryDataRequested += delegate { ShowHistory(); };
            _window.ModeSwitchRequested += delegate(TimerMode m) { SwitchMode(m); };
            _window.SettingsChanged += delegate(AppSettings s) { ApplySettings(s); };
            _window.NoteChanged += delegate(string note) { SetNoteDraft(note); };
            _window.MusicTestRequested += delegate(MusicLink link) { TestMusicLink(link); };

            _breakWindow.Completed += delegate(bool skipped) { OnBreakCompleted(skipped); };

            _tray.ToggleVisibility += delegate { ToggleVisibility(); };
            _tray.StartPause += delegate { HandleStartPause(); };
            _tray.ModeSelected += delegate(TimerMode m) { SwitchMode(m); };
            _tray.ShowHistory += delegate { ShowHistory(); };
            _tray.OpenMusic += delegate { OpenMusic(); };
            _tray.ToggleCompact += delegate { ToggleCompact(); };
            _tray.StartupChanged += delegate(bool enabled) { SetStartup(enabled); };
            _tray.TopmostChanged += delegate(bool enabled) { SetTopmost(enabled); };
            _tray.ExitRequested += delegate { Shutdown(); };
        }

        // ------------------------------------------------------------------
        // System suspend / resume
        // ------------------------------------------------------------------
        private void SubscribeSystemEvents()
        {
            try
            {
                SystemEvents.SessionSwitch += OnSessionSwitch;
                SystemEvents.PowerModeChanged += OnPowerModeChanged;
            }
            catch
            {
                // SystemEvents may fail in some hosting scenarios; the long-gap
                // guard in SessionEngine is the fallback.
            }
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            _window.Dispatcher.BeginInvoke(new Action(delegate
            {
                if (e.Reason == SessionSwitchReason.SessionLock) OnSystemSuspend();
                else if (e.Reason == SessionSwitchReason.SessionUnlock) OnSystemResume();
            }));
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            _window.Dispatcher.BeginInvoke(new Action(delegate
            {
                if (e.Mode == PowerModes.Suspend) OnSystemSuspend();
                else if (e.Mode == PowerModes.Resume) OnSystemResume();
            }));
        }

        private void OnSystemSuspend()
        {
            long now = NowMs();
            bool wasRunning = _activeEngine.Running;
            bool hadOverlay = _eyeRestActive;

            if (hadOverlay && _breakWindow != null)
                _breakWindow.ForceFinishAndCredit();

            _activeEngine.Pause(now);
            _resumeAfterSystemPause = wasRunning || hadOverlay;
            _eyeRestActive = false;

            SaveState();
            SaveHistory();
            RenderUi();
        }

        private void OnSystemResume()
        {
            if (_resumeAfterSystemPause && !_shuttingDown)
            {
                _activeEngine.Start(NowMs());
                if (_activeEngine.Mode == TimerMode.Pomodoro && _activeEngine.Phase == TimerPhase.Focus)
                    EnsureMusicOpen();
            }
            _resumeAfterSystemPause = false;
            RenderUi();
        }

        // ------------------------------------------------------------------
        // Timer tick
        // ------------------------------------------------------------------
        private void OnTick(object sender, EventArgs e)
        {
            if (_shuttingDown) return;
            long now = NowMs();
            TimerTickResult result = _activeEngine.Tick(now);

            if (result.LongGapAutoPaused)
            {
                _resumeAfterSystemPause = false;
            }

            foreach (TimerEvent ev in result.Events)
                HandleEngineEvent(ev);

            string dateKey = HistoryData.DateKey(DateTime.Now);
            if (dateKey != _lastDateKey)
                _lastDateKey = dateKey;

            RenderUi();
            if (_historyStore.IsDirty) SaveHistory();
        }

        private void HandleEngineEvent(TimerEvent ev)
        {
            if (ev == null) return;
            switch (ev.Type)
            {
                case TimerEventType.FocusCompleted:
                    FinalizeAttempt(FocusStatus.Completed, ev.PlannedSeconds);
                    PlayChime();
                    NotifyIfHidden("专注完成", "休息一下吧");
                    break;

                case TimerEventType.FocusStarted:
                    OnFocusStarted(ev);
                    break;

                case TimerEventType.FocusSkipped:
                    FinalizeAttempt(FocusStatus.Skipped, ev.ActualSeconds);
                    break;

                case TimerEventType.FocusReset:
                    FinalizeAttempt(FocusStatus.Reset, ev.ActualSeconds);
                    break;

                case TimerEventType.BreakCompleted:
                    PlayChime();
                    NotifyIfHidden("休息结束", "准备开始下一轮专注");
                    break;

                case TimerEventType.EyeRestStarted:
                    OpenEyeRestOverlay();
                    break;

                case TimerEventType.EyeRestCompleted:
                    OnEyeRestCompleted(ev.Skipped);
                    break;
            }
        }

        private void OnFocusStarted(TimerEvent ev)
        {
            if (_attemptStartLocal == null)
            {
                _attemptStartLocal = DateTime.Now;
                _attemptPlannedSeconds = ev.PlannedSeconds > 0 ? ev.PlannedSeconds : _pomodoroEngine.PhaseDurationSeconds(TimerPhase.Focus);
                _state.Session.AttemptStartedAtUtc = HistoryData.ToIsoUtc(_attemptStartLocal.Value);
            }
            EnsureMusicOpen();
            SaveState();
        }

        private void EnsureMusicOpen()
        {
            if (_settings.MusicLinks == null || _settings.MusicLinks.Count == 0) return;
            _musicLauncher.EnsureOpenAsync(
                _settings.MusicLinks,
                delegate(string info) { DispatchToast(info); },
                delegate(string err) { DispatchToast(err); });
        }

        private void DispatchToast(string message)
        {
            _window.Dispatcher.BeginInvoke(new Action(delegate
            {
                _tray.ShowBalloon("番茄小憩", message);
            }));
        }

        private void OpenEyeRestOverlay()
        {
            _eyeRestActive = true;
            _window.SetInteractive(false);
            PlayChime();
            _breakWindow.Start(_settings.EyeRestSeconds, _settings.StrictEyeRest, _settings.FullscreenRest);
        }

        private void OnBreakCompleted(bool skipped)
        {
            if (!_eyeRestActive) return;
            _eyeRestActive = false;
            _window.SetInteractive(true);

            TimerEvent ev = _eyeEngine.CompleteEyeRest(NowMs(), skipped);
            if (!skipped)
            {
                _historyStore.AddEyeRest(DateTime.Now);
                PlayChime();
            }
            SaveHistory();
            SaveState();
            RenderUi();
        }

        private void OnEyeRestCompleted(bool skipped)
        {
            if (!skipped)
            {
                _historyStore.AddEyeRest(DateTime.Now);
                PlayChime();
            }
            SaveHistory();
            SaveState();
        }

        // ------------------------------------------------------------------
        // User actions
        // ------------------------------------------------------------------
        private void HandleStartPause()
        {
            if (_eyeRestActive) return;
            long now = NowMs();
            if (_activeEngine.Running)
            {
                _activeEngine.Pause(now);
            }
            else
            {
                _activeEngine.Start(now);
                if (_activeEngine.Mode == TimerMode.Pomodoro && _activeEngine.Phase == TimerPhase.Focus)
                    OnFocusStarted(new TimerEvent(TimerEventType.FocusStarted, TimerPhase.Focus, TimerPhase.Focus,
                        _pomodoroEngine.PhaseDurationSeconds(TimerPhase.Focus), 0, false));
            }
            SaveState();
            RenderUi();
        }

        private void HandleReset()
        {
            if (_eyeRestActive) return;
            TimerEvent ev = _activeEngine.Reset(NowMs());
            if (ev != null) HandleEngineEvent(ev);
            SaveState();
            RenderUi();
        }

        private void HandleSkip()
        {
            if (_eyeRestActive) return;
            TimerEvent ev = _activeEngine.Skip(NowMs());
            if (ev != null) HandleEngineEvent(ev);
            SaveState();
            RenderUi();
        }

        private void SwitchMode(TimerMode mode)
        {
            if (_eyeRestActive) return;
            if (mode == _activeEngine.Mode) return;
            _activeEngine.Pause(NowMs());
            _activeEngine = mode == TimerMode.Pomodoro ? _pomodoroEngine : _eyeEngine;
            SaveState();
            _tray.SetMode(mode);
            RenderUi();
        }

        private void HandleHide()
        {
            _window.Hide();
            _tray.SetVisible(false);
            if (!_hintShown)
            {
                _hintShown = true;
                _tray.ShowBalloon("番茄小憩", "已最小化到托盘，计时继续");
            }
        }

        private void ToggleVisibility()
        {
            if (_window.IsVisible)
            {
                _window.Hide();
                _tray.SetVisible(false);
            }
            else
            {
                _window.BringToFront();
                _tray.SetVisible(true);
            }
        }

        private void ToggleCompact()
        {
            bool next = !_window.IsCompact;
            _window.SetCompact(next);
            _tray.SetCompact(next);
            SaveState();
        }

        private void ToggleTopmost()
        {
            SetTopmost(!_settings.AlwaysOnTop);
        }

        private void SetTopmost(bool on)
        {
            _settings.AlwaysOnTop = on;
            _state.Settings.AlwaysOnTop = on;
            _window.SetAlwaysOnTop(on);
            _tray.SetTopmost(on);
            SaveState();
        }

        private void ShowHistory()
        {
            _window.BringToFront();
            _window.SetCompact(false);
            _tray.SetCompact(false);
            _window.BuildHistory(_historyStore.Data);
            _window.ShowHistoryView();
            RenderUi();
        }

        private void OpenMusic()
        {
            if (_settings.MusicLinks == null || _settings.MusicLinks.Count == 0)
            {
                _tray.ShowBalloon("番茄小憩", "还没有配置背景音乐链接");
                return;
            }
            _musicLauncher.ForceOpenAsync(_settings.MusicLinks,
                delegate(string info) { DispatchToast(info); },
                delegate(string err) { DispatchToast(err); });
        }

        private void TestMusicLink(MusicLink link)
        {
            if (link == null || !AppSettings.IsValidUrl(link.Url))
            {
                _tray.ShowBalloon("番茄小憩", "链接无效，请输入 http/https 地址");
                return;
            }
            _musicLauncher.ForceOpenAsync(new List<MusicLink> { link },
                delegate(string info) { DispatchToast(info); },
                delegate(string err) { DispatchToast(err); });
        }

        // ------------------------------------------------------------------
        // Settings, note, startup
        // ------------------------------------------------------------------
        private void ApplySettings(AppSettings settings)
        {
            if (settings == null) return;
            _settings = settings.Clone();
            _settings.Normalize();

            long now = NowMs();
            _pomodoroEngine.ApplySettings(_settings, now);
            _eyeEngine.ApplySettings(_settings, now);

            _state.Settings = _settings.Clone();
            string sig = MusicSignature();
            if (sig != _lastMusicSignature)
            {
                _musicLauncher.Invalidate();
                _lastMusicSignature = sig;
            }
            SetStartup(_settings.StartWithWindows);
            _window.SetAlwaysOnTop(_settings.AlwaysOnTop);
            _tray.SetTopmost(_settings.AlwaysOnTop);

            SaveState();
            RenderUi();
        }

        private string MusicSignature()
        {
            var sb = new System.Text.StringBuilder();
            if (_settings.MusicLinks != null)
            {
                foreach (MusicLink l in _settings.MusicLinks)
                {
                    sb.Append(l.Enabled ? '1' : '0');
                    sb.Append('|');
                    sb.Append(l.Url);
                    sb.Append(';');
                }
            }
            return sb.ToString();
        }

        private void SetNoteDraft(string note)
        {
            _noteDraft = note ?? string.Empty;
            _state.Session.NoteDraft = _noteDraft;
            _window.SetNoteDraft(_noteDraft);
            DebounceSaveState();
        }

        private void SetStartup(bool enabled)
        {
            _settings.StartWithWindows = enabled;
            _state.Settings.StartWithWindows = enabled;
            bool ok = enabled
                ? _startupManager.Enable(CurrentExePath())
                : _startupManager.Disable();
            if (!ok)
            {
                _settings.StartWithWindows = false;
                _state.Settings.StartWithWindows = false;
                _tray.ShowBalloon("番茄小憩", "开机自启动写入失败");
            }
            _tray.SetStartup(_settings.StartWithWindows);
            SaveState();
        }

        private void EnsureStartupState()
        {
            // Keep the registry in sync with the persisted setting on every launch,
            // and fix a stale path when the exe has been moved.
            if (_settings.StartWithWindows)
            {
                bool ok = _startupManager.Enable(CurrentExePath());
                if (!ok)
                {
                    _settings.StartWithWindows = false;
                    _state.Settings.StartWithWindows = false;
                    SaveState();
                }
            }
            else
            {
                _startupManager.Disable();
            }
            _tray.SetStartup(_settings.StartWithWindows);
        }

        private void HandleResetDefaults()
        {
            _settings = AppSettings.CreateDefaults();
            _state.Settings = _settings.Clone();
            _state.Session.NoteDraft = string.Empty;
            _noteDraft = string.Empty;
            _window.SetNoteDraft(string.Empty);
            long now = NowMs();
            _pomodoroEngine.ApplySettings(_settings, now);
            _eyeEngine.ApplySettings(_settings, now);
            _musicLauncher.Invalidate();
            _lastMusicSignature = MusicSignature();
            SetStartup(_settings.StartWithWindows);
            _window.SetAlwaysOnTop(_settings.AlwaysOnTop);
            _tray.SetTopmost(_settings.AlwaysOnTop);
            _window.BuildSettings(_settings.Clone());
            SaveState();
            RenderUi();
            _tray.ShowBalloon("番茄小憩", "已恢复默认设置");
        }

        // ------------------------------------------------------------------
        // History finalization and export
        // ------------------------------------------------------------------
        private void FinalizeAttempt(string status, int actualSeconds)
        {
            if (_attemptStartLocal == null) return;
            if (_window != null && _window.IsNoteEditorOpen) _window.CloseNoteEditor(true);

            int actual = Math.Max(0, actualSeconds);
            _historyStore.AddFocusSession(_attemptStartLocal.Value, DateTime.Now,
                _attemptPlannedSeconds, actual, status, _noteDraft);

            _attemptStartLocal = null;
            _noteDraft = string.Empty;
            _state.Session.NoteDraft = string.Empty;
            _state.Session.AttemptStartedAtUtc = null;
            if (_window != null) _window.SetNoteDraft(string.Empty);
        }

        private void ExportDetail()
        {
            string path = ChooseCsvPath("PomoRest-专注明细-" + CsvExporter.FileStamp(DateTime.Now) + ".csv");
            if (path == null) return;
            try
            {
                CsvExporter.ExportDetail(_historyStore.Data, path);
                _tray.ShowBalloon("番茄小憩", "已导出明细：" + Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                _tray.ShowBalloon("番茄小憩", "导出失败：" + ex.Message);
            }
        }

        private void ExportSummary()
        {
            string path = ChooseCsvPath("PomoRest-每日汇总-" + CsvExporter.FileStamp(DateTime.Now) + ".csv");
            if (path == null) return;
            try
            {
                CsvExporter.ExportSummary(_historyStore.Data, path);
                _tray.ShowBalloon("番茄小憩", "已导出汇总：" + Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                _tray.ShowBalloon("番茄小憩", "导出失败：" + ex.Message);
            }
        }

        private string ChooseCsvPath(string defaultName)
        {
            var dlg = new SaveFileDialog
            {
                Title = "导出记录",
                FileName = defaultName,
                Filter = "CSV 文件 (*.csv)|*.csv",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (dlg.ShowDialog() == true) return dlg.FileName;
            return null;
        }

        // ------------------------------------------------------------------
        // Rendering and persistence
        // ------------------------------------------------------------------
        private void RenderUi()
        {
            if (_window == null || _shuttingDown) return;
            TodaySummary stats = _historyStore.GetTodaySummary(DateTime.Now);
            SessionSnapshot snap = _activeEngine.GetSnapshot(stats.EyeRests);
            _window.Render(snap, stats, _settings, _noteDraft);

            _tray.SetRunning(_activeEngine.Running);
            _tray.SetMode(_activeEngine.Mode);
            _tray.SetTooltip("番茄小憩 · " + TimeFormat.PhaseLabel(_activeEngine.Phase) + " " +
                             TimeFormat.MmSs(snap.RemainingSeconds));
        }

        private void SaveState()
        {
            if (_window == null || _state == null) return;
            _state.Settings = _settings.Clone();
            _state.Window.Left = _window.Left;
            _state.Window.Top = _window.Top;
            _state.Window.Compact = _window.IsCompact;
            _state.Session = BuildSessionState();
            _stateStore.Save(_state);
        }

        private SessionStateData BuildSessionState()
        {
            var s = new SessionStateData();
            s.Mode = _activeEngine.Mode == TimerMode.Pomodoro ? "pomodoro" : "eyecare";
            s.Phase = PhaseToString(_activeEngine.Phase);
            int remaining = (int)Math.Ceiling(_activeEngine.RemainingMs / 1000.0);
            if (_activeEngine.Phase == TimerPhase.EyeRest)
            {
                // Never restore into an open rest overlay on next launch.
                s.Phase = PhaseToString(TimerPhase.EyeWork);
                remaining = _eyeEngine.PhaseDurationSeconds(TimerPhase.EyeWork);
            }
            s.RemainingSeconds = remaining;
            s.CompletedInCycle = _pomodoroEngine.CompletedInCycle;
            s.NoteDraft = _noteDraft;
            s.AttemptStartedAtUtc = _attemptStartLocal.HasValue ? HistoryData.ToIsoUtc(_attemptStartLocal.Value) : null;
            return s;
        }

        private void DebounceSaveState()
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void SaveHistory()
        {
            _historyStore.Save();
        }

        private void PlayChime()
        {
            if (_settings.SoundEnabled) _chime.Play();
        }

        private void NotifyIfHidden(string title, string message)
        {
            if (!_window.IsVisible || !_window.IsActive)
                _tray.ShowBalloon(title, message);
        }

        private static string CurrentExePath()
        {
            return Process.GetCurrentProcess().MainModule.FileName;
        }

        private long NowMs()
        {
            return _stopwatch.ElapsedMilliseconds;
        }

        private static TimerMode ParseMode(string s)
        {
            return (s != null && s == "eyecare") ? TimerMode.EyeCare : TimerMode.Pomodoro;
        }

        private static TimerPhase ParsePhase(string s)
        {
            switch (s)
            {
                case "focus": return TimerPhase.Focus;
                case "shortbreak": return TimerPhase.ShortBreak;
                case "longbreak": return TimerPhase.LongBreak;
                case "eyework": return TimerPhase.EyeWork;
                case "eyerest": return TimerPhase.EyeRest;
                default: return TimerPhase.Focus;
            }
        }

        private static string PhaseToString(TimerPhase phase)
        {
            switch (phase)
            {
                case TimerPhase.Focus: return "focus";
                case TimerPhase.ShortBreak: return "shortbreak";
                case TimerPhase.LongBreak: return "longbreak";
                case TimerPhase.EyeWork: return "eyework";
                case TimerPhase.EyeRest: return "eyerest";
                default: return "focus";
            }
        }

        // ------------------------------------------------------------------
        // Shutdown
        // ------------------------------------------------------------------
        public void Shutdown()
        {
            if (_shuttingDown) return;
            _shuttingDown = true;

            try
            {
                if (_eyeRestActive && _breakWindow != null)
                    _breakWindow.ForceFinishAndCredit();

                if (_attemptStartLocal != null && _pomodoroEngine.Phase == TimerPhase.Focus)
                {
                    int planned = _attemptPlannedSeconds;
                    int remaining = (int)Math.Ceiling(_pomodoroEngine.RemainingMs / 1000.0);
                    int actual = Math.Max(0, planned - remaining);
                    FinalizeAttempt(FocusStatus.Interrupted, actual);
                }
            }
            catch { }

            SaveState();
            SaveHistory();

            try
            {
                SystemEvents.SessionSwitch -= OnSessionSwitch;
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            }
            catch { }

            _tickTimer.Stop();
            _topmostTimer.Stop();
            _saveTimer.Stop();

            if (_breakWindow != null)
            {
                _breakWindow.ForceClose();
            }
            if (_tray != null) _tray.Dispose();
            if (_window != null) _window.RequestClose();

            _app.Shutdown();
        }
    }
}
