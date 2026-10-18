using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using PomoRest.Core;
using PomoRest.Services;

namespace PomoRest.UI
{
    public partial class MainWindow : Window
    {
        // Events consumed by the controller.
        public event EventHandler StartPauseRequested;
        public event EventHandler ResetRequested;
        public event EventHandler SkipRequested;
        public event EventHandler HideRequested;
        public event EventHandler ExitRequested;
        public event EventHandler ExportDetailRequested;
        public event EventHandler ExportSummaryRequested;
        public event EventHandler ResetDefaultsRequested;
        public event EventHandler ShowSignalReceived;
        public event EventHandler CompactToggleRequested;
        public event EventHandler TopmostToggleRequested;
        public event Action<TimerMode> ModeSwitchRequested;
        public event Action<AppSettings> SettingsChanged;
        public event Action<string> NoteChanged;
        public event Action<MusicLink> MusicTestRequested;

        private Grid _header;
        private Grid _modeSwitch;
        private Border _modeIndicator;
        private Button _pomodoroTab;
        private Button _eyeTab;
        private TextBlock _titleText;
        private TextBlock _statusText;
        private Ellipse _statusDot;
        private Button _pinButton;
        private Button _compactButton;
        private Button _settingsButton;
        private Button _hideButton;

        private Grid _mainView;
        private Path _progressArc;
        private Path _compactArc;
        private TextBlock _phaseLabel;
        private TextBlock _timeText;
        private TextBlock _subStatusText;
        private StackPanel _dotsPanel;
        private TextBlock _eyeInfoText;
        private Button _noteButton;
        private TextBlock _noteLabel;
        private Button _resetButton;
        private Button _primaryButton;
        private Button _skipButton;

        private Grid _historyView;
        private TextBlock _historySummaryText;
        private StackPanel _historyList;
        private Button _historyBackButton;
        private Button _exportDetailButton;
        private Button _exportSummaryButton;

        private Grid _settingsView;
        private StackPanel _settingsStack;
        private Button _settingsBackButton;
        private Button _resetDefaultsButton;
        private Button _exitButton;

        private Grid _footer;
        private Button _statsButton;
        private TextBlock _statsText;

        private Grid _noteOverlay;
        private TextBox _noteBox;
        private TextBlock _noteCount;
        private Button _noteCancelButton;
        private Button _noteSaveButton;

        private Grid _compactView;
        private TextBlock _compactTime;
        private TextBlock _compactStatus;
        private Button _compactPrimaryButton;

        private SessionSnapshot _snap;
        private TodaySummary _stats = new TodaySummary();
        private AppSettings _settings = AppSettings.CreateDefaults();
        private AppSettings _settingsEdit = AppSettings.CreateDefaults();
        private FrameworkElement _root;
        private string _noteDraft = string.Empty;
        private string _noteBeforeEdit = string.Empty;
        private bool _compact;
        private bool _allowClose;
        private bool _alwaysOnTop = true;
        private IntPtr _hwnd = IntPtr.Zero;
        private int _showMessageId;
        private int _taskbarCreatedId;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AllowsTransparency = true;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Background = Brushes.Transparent;
            ShowInTaskbar = true;
            Topmost = true;
            Width = 352;
            Height = 560;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            FontFamily = new FontFamily("Microsoft YaHei UI");
            Title = "番茄小憩";

            Content = (UIElement)UiResources.LoadXaml("MainWindow.xaml");
            _root = (FrameworkElement)Content;
            Icon = UiResources.LoadWindowIconFrame();

            FindElements();
            WireEvents();

            SourceInitialized += OnSourceInitialized;
            Deactivated += delegate { if (_alwaysOnTop) EnforceTopmost(); };
            Closing += OnClosing;
            SizeChanged += delegate { UpdateModeIndicator(); };
            PreviewKeyDown += OnPreviewKeyDown;
            MouseLeftButtonDown += OnRootMouseDown;
        }

        private void FindElements()
        {
            _header = (Grid)_root.FindName("Header");
            _modeSwitch = (Grid)_root.FindName("ModeSwitch");
            _modeIndicator = (Border)_root.FindName("ModeIndicator");
            _pomodoroTab = (Button)_root.FindName("PomodoroTab");
            _eyeTab = (Button)_root.FindName("EyeTab");
            _titleText = (TextBlock)_root.FindName("TitleText");
            _statusText = (TextBlock)_root.FindName("StatusText");
            _statusDot = (Ellipse)_root.FindName("StatusDot");
            _pinButton = (Button)_root.FindName("PinButton");
            _compactButton = (Button)_root.FindName("CompactButton");
            _settingsButton = (Button)_root.FindName("SettingsButton");
            _hideButton = (Button)_root.FindName("HideButton");

            _mainView = (Grid)_root.FindName("MainView");
            _progressArc = (Path)_root.FindName("ProgressArc");
            _compactArc = (Path)_root.FindName("CompactArc");
            _phaseLabel = (TextBlock)_root.FindName("PhaseLabel");
            _timeText = (TextBlock)_root.FindName("TimeText");
            _subStatusText = (TextBlock)_root.FindName("SubStatusText");
            _dotsPanel = (StackPanel)_root.FindName("DotsPanel");
            _eyeInfoText = (TextBlock)_root.FindName("EyeInfoText");
            _noteButton = (Button)_root.FindName("NoteButton");
            _noteLabel = (TextBlock)_root.FindName("NoteLabel");
            _resetButton = (Button)_root.FindName("ResetButton");
            _primaryButton = (Button)_root.FindName("PrimaryButton");
            _skipButton = (Button)_root.FindName("SkipButton");

            _historyView = (Grid)_root.FindName("HistoryView");
            _historySummaryText = (TextBlock)_root.FindName("HistorySummaryText");
            _historyList = (StackPanel)_root.FindName("HistoryList");
            _historyBackButton = (Button)_root.FindName("HistoryBackButton");
            _exportDetailButton = (Button)_root.FindName("ExportDetailButton");
            _exportSummaryButton = (Button)_root.FindName("ExportSummaryButton");

            _settingsView = (Grid)_root.FindName("SettingsView");
            _settingsStack = (StackPanel)_root.FindName("SettingsStack");
            _settingsBackButton = (Button)_root.FindName("SettingsBackButton");
            _resetDefaultsButton = (Button)_root.FindName("ResetDefaultsButton");
            _exitButton = (Button)_root.FindName("ExitButton");

            _footer = (Grid)_root.FindName("Footer");
            _statsButton = (Button)_root.FindName("StatsButton");
            _statsText = (TextBlock)_root.FindName("StatsText");

            _noteOverlay = (Grid)_root.FindName("NoteOverlay");
            _noteBox = (TextBox)_root.FindName("NoteBox");
            _noteCount = (TextBlock)_root.FindName("NoteCount");
            _noteCancelButton = (Button)_root.FindName("NoteCancelButton");
            _noteSaveButton = (Button)_root.FindName("NoteSaveButton");

            _compactView = (Grid)_root.FindName("CompactView");
            _compactTime = (TextBlock)_root.FindName("CompactTime");
            _compactStatus = (TextBlock)_root.FindName("CompactStatus");
            _compactPrimaryButton = (Button)_root.FindName("CompactPrimaryButton");
        }

        private void WireEvents()
        {
            _pinButton.Click += delegate { if (TopmostToggleRequested != null) TopmostToggleRequested(this, EventArgs.Empty); };
            _compactButton.Click += delegate { if (CompactToggleRequested != null) CompactToggleRequested(this, EventArgs.Empty); };
            _settingsButton.Click += delegate { ShowSettingsView(); };
            _hideButton.Click += delegate { if (HideRequested != null) HideRequested(this, EventArgs.Empty); };

            _pomodoroTab.Click += delegate { if (ModeSwitchRequested != null) ModeSwitchRequested(TimerMode.Pomodoro); };
            _eyeTab.Click += delegate { if (ModeSwitchRequested != null) ModeSwitchRequested(TimerMode.EyeCare); };

            _primaryButton.Click += delegate { if (StartPauseRequested != null) StartPauseRequested(this, EventArgs.Empty); };
            _resetButton.Click += delegate { if (ResetRequested != null) ResetRequested(this, EventArgs.Empty); };
            _skipButton.Click += delegate { if (SkipRequested != null) SkipRequested(this, EventArgs.Empty); };

            _noteButton.Click += delegate { ShowNoteEditor(); };

            _historyBackButton.Click += delegate { ShowMainView(); };
            _settingsBackButton.Click += delegate { ShowMainView(); };
            _statsButton.Click += delegate { if (HistoryDataRequested != null) HistoryDataRequested(this, EventArgs.Empty); };

            _exportDetailButton.Click += delegate { if (ExportDetailRequested != null) ExportDetailRequested(this, EventArgs.Empty); };
            _exportSummaryButton.Click += delegate { if (ExportSummaryRequested != null) ExportSummaryRequested(this, EventArgs.Empty); };
            _resetDefaultsButton.Click += delegate { if (ResetDefaultsRequested != null) ResetDefaultsRequested(this, EventArgs.Empty); };
            _exitButton.Click += delegate { if (ExitRequested != null) ExitRequested(this, EventArgs.Empty); };

            _noteBox.TextChanged += delegate
            {
                _noteCount.Text = _noteBox.Text.Length + " / 200";
                if (NoteChanged != null) NoteChanged(_noteBox.Text);
            };
            _noteSaveButton.Click += delegate { CloseNoteEditor(true); };
            _noteCancelButton.Click += delegate { CloseNoteEditor(false); };

            _compactPrimaryButton.Click += delegate { if (StartPauseRequested != null) StartPauseRequested(this, EventArgs.Empty); };
        }

        public event EventHandler HistoryDataRequested;
        public event EventHandler TaskbarCreated;

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            _showMessageId = NativeMethods.RegisterWindowMessage("PomoRest.ShowWindow");
            _taskbarCreatedId = NativeMethods.RegisterWindowMessage("TaskbarCreated");
            HwndSource source = HwndSource.FromHwnd(_hwnd);
            if (source != null) source.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == _showMessageId)
            {
                handled = true;
                if (ShowSignalReceived != null) ShowSignalReceived(this, EventArgs.Empty);
            }
            else if (msg == _taskbarCreatedId)
            {
                handled = true;
                if (TaskbarCreated != null) TaskbarCreated(this, EventArgs.Empty);
            }
            return IntPtr.Zero;
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                Hide();
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (IsTextInputFocused())
            {
                if (e.Key == Key.Escape) CloseNoteEditor(false);
                return;
            }

            switch (e.Key)
            {
                case Key.Space: if (StartPauseRequested != null) StartPauseRequested(this, EventArgs.Empty); e.Handled = true; break;
                case Key.R: if (ResetRequested != null) ResetRequested(this, EventArgs.Empty); e.Handled = true; break;
                case Key.S: if (SkipRequested != null) SkipRequested(this, EventArgs.Empty); e.Handled = true; break;
                case Key.D1: if (ModeSwitchRequested != null) ModeSwitchRequested(TimerMode.Pomodoro); e.Handled = true; break;
                case Key.D2: if (ModeSwitchRequested != null) ModeSwitchRequested(TimerMode.EyeCare); e.Handled = true; break;
                case Key.Escape: if (HideRequested != null) HideRequested(this, EventArgs.Empty); e.Handled = true; break;
            }
        }

        private bool IsTextInputFocused()
        {
            if (Keyboard.FocusedElement is TextBox) return true;
            return false;
        }

        private void OnRootMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (IsInteractiveSource(e.OriginalSource)) return;

            if (e.ClickCount == 2)
            {
                if (CompactToggleRequested != null) CompactToggleRequested(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }

            try
            {
                DragMove();
            }
            catch
            {
                // DragMove can throw if the button is released during the move.
            }
        }

        private static bool IsInteractiveSource(object source)
        {
            DependencyObject dep = source as DependencyObject;
            while (dep != null)
            {
                if (dep is ButtonBase || dep is TextBox || dep is ScrollBar || dep is ScrollViewer)
                    return true;
                dep = VisualTreeHelper.GetParent(dep);
            }
            return false;
        }

        public void EnforceTopmost()
        {
            if (_hwnd != IntPtr.Zero)
                NativeMethods.EnforceTopmost(_hwnd);
        }

        public IntPtr WindowHandle { get { return _hwnd; } }

        public bool IsCompact { get { return _compact; } }

        public void ApplyWindowBounds(double left, double top, bool compact)
        {
            bool hasLeft = !double.IsNaN(left) && !double.IsInfinity(left) && left > -90000;
            bool hasTop = !double.IsNaN(top) && !double.IsInfinity(top) && top > -90000;

            if (hasLeft) Left = left;
            else Left = SystemParameters.WorkArea.Right - Width - 24;
            if (hasTop) Top = top;
            else Top = SystemParameters.WorkArea.Top + 24;

            SetCompact(compact);
        }

        public void SetCompact(bool compact)
        {
            _compact = compact;
            Width = compact ? 300 : 352;
            Height = compact ? 118 : 560;

            _header.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            _modeSwitch.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            _footer.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            _noteOverlay.Visibility = Visibility.Collapsed;

            if (compact)
            {
                _mainView.Visibility = Visibility.Collapsed;
                _historyView.Visibility = Visibility.Collapsed;
                _settingsView.Visibility = Visibility.Collapsed;
            }
            else
            {
                _mainView.Visibility = Visibility.Visible;
                _historyView.Visibility = Visibility.Collapsed;
                _settingsView.Visibility = Visibility.Collapsed;
            }

            _compactView.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
            EnforceTopmost();
        }

        public void ShowMainView()
        {
            _modeSwitch.Visibility = Visibility.Visible;
            _mainView.Visibility = Visibility.Visible;
            _historyView.Visibility = Visibility.Collapsed;
            _settingsView.Visibility = Visibility.Collapsed;
        }

        public void ShowHistoryView()
        {
            if (_compact) return;
            _modeSwitch.Visibility = Visibility.Collapsed;
            _mainView.Visibility = Visibility.Collapsed;
            _historyView.Visibility = Visibility.Visible;
            _settingsView.Visibility = Visibility.Collapsed;
            _noteOverlay.Visibility = Visibility.Collapsed;
        }

        public void ShowSettingsView()
        {
            if (_compact) return;
            BuildSettings(_settings.Clone());
            _modeSwitch.Visibility = Visibility.Collapsed;
            _mainView.Visibility = Visibility.Collapsed;
            _historyView.Visibility = Visibility.Collapsed;
            _settingsView.Visibility = Visibility.Visible;
            _noteOverlay.Visibility = Visibility.Collapsed;
        }

        public void Render(SessionSnapshot snap, TodaySummary stats, AppSettings settings, string noteDraft)
        {
            _snap = snap;
            _stats = stats ?? new TodaySummary();
            _settings = settings ?? AppSettings.CreateDefaults();
            _noteDraft = noteDraft ?? string.Empty;

            Brush accent = (_snap.Mode == TimerMode.Pomodoro)
                ? (Brush)FindResource("TomatoBrush")
                : (Brush)FindResource("EyeBrush");
            Brush muted = (Brush)FindResource("MutedBrush");

            _phaseLabel.Text = TimeFormat.PhaseLabel(_snap.Phase);
            _timeText.Text = TimeFormat.MmSs(_snap.RemainingSeconds);

            bool eyeRest = _snap.Phase == TimerPhase.EyeRest;
            if (eyeRest)
            {
                _statusText.Text = "休息中";
                _statusDot.Fill = accent;
            }
            else if (_snap.Running)
            {
                _statusText.Text = "运行中";
                _statusDot.Fill = accent;
            }
            else
            {
                _statusText.Text = "已暂停";
                _statusDot.Fill = muted;
            }

            _subStatusText.Text = string.Empty;
            if (_snap.Mode == TimerMode.EyeCare && _snap.Phase == TimerPhase.EyeWork)
                _subStatusText.Text = "20-20-20";

            bool focus = _snap.Mode == TimerMode.Pomodoro && _snap.Phase == TimerPhase.Focus;
            _noteButton.Visibility = focus ? Visibility.Visible : Visibility.Collapsed;
            _noteLabel.Text = string.IsNullOrEmpty(_noteDraft)
                ? "  添加本次备注"
                : "  " + Truncate(_noteDraft, 18);

            // Primary action text
            if (_snap.Running)
            {
                _primaryButton.Content = "暂停";
            }
            else
            {
                bool fresh = Math.Abs(_snap.RemainingMs - _snap.DurationMs) < 1000;
                _primaryButton.Content = fresh ? "开始" : "继续";
            }

            if (_snap.Mode == TimerMode.EyeCare && _snap.Phase == TimerPhase.EyeWork)
                _skipButton.Content = "休息";
            else
                _skipButton.Content = "跳过";

            // Session dots (pomodoro) or eye info (eye mode)
            if (_snap.Mode == TimerMode.Pomodoro)
            {
                _dotsPanel.Visibility = Visibility.Visible;
                _eyeInfoText.Visibility = Visibility.Collapsed;
                BuildDots(accent);
            }
            else
            {
                _dotsPanel.Visibility = Visibility.Collapsed;
                _eyeInfoText.Visibility = Visibility.Visible;
                _eyeInfoText.Text = "每 " + _settings.EyeIntervalMinutes + " 分钟 · 远眺 " +
                    _settings.EyeRestSeconds + " 秒 · 今日 " + _snap.EyeRestsToday + " 次";
            }

            _statsText.Text = "今日 " + _stats.FocusCompleted + " 轮 · " + _stats.FocusMinutes +
                              " 分钟 · 护眼 " + _stats.EyeRests + " 次";

            // Colors
            _progressArc.Stroke = accent;
            _compactArc.Stroke = accent;
            _primaryButton.Background = accent;
            _compactPrimaryButton.Background = accent;
            _modeIndicator.Background = accent;
            _pomodoroTab.Foreground = _snap.Mode == TimerMode.Pomodoro ? (Brush)FindResource("TextBrush") : muted;
            _eyeTab.Foreground = _snap.Mode == TimerMode.EyeCare ? (Brush)FindResource("TextBrush") : muted;

            double progress = Clamp01(_snap.RemainingMs / Math.Max(1, _snap.DurationMs));
            _progressArc.Data = BuildArc(196, 10, progress);
            _compactArc.Data = BuildArc(52, 6, progress);

            // Compact view
            _compactTime.Text = _timeText.Text;
            _compactStatus.Text = _statusText.Text + " · " + _phaseLabel.Text;
            _compactPrimaryButton.Content = _snap.Running ? "\uE769" : "\uE768";

            UpdateModeIndicator();
        }

        private void BuildDots(Brush accent)
        {
            _dotsPanel.Children.Clear();
            int count = Math.Max(2, _settings.LongBreakEvery);
            int filled = _snap.CompletedInCycle;
            if (filled > count) filled = count;
            if (_snap.Phase == TimerPhase.LongBreak) filled = count;

            for (int i = 0; i < count; i++)
            {
                var dot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = i < filled ? accent : (Brush)FindResource("TrackBrush"),
                    Margin = new Thickness(0, 0, i < count - 1 ? 7 : 0, 0)
                };
                _dotsPanel.Children.Add(dot);
            }
        }

        private void UpdateModeIndicator()
        {
            if (_modeSwitch == null || _snap == null) return;
            double width = _modeSwitch.ActualWidth;
            if (width < 40) width = 280;
            double half = (width - 6) / 2.0;
            _modeIndicator.Width = half;
            double left = (_snap.Mode == TimerMode.EyeCare) ? half : 0;
            _modeIndicator.Margin = new Thickness(left, 0, 0, 0);
        }

        public void ShowNoteEditor()
        {
            _noteBeforeEdit = _noteDraft;
            _noteBox.Text = _noteDraft;
            _noteCount.Text = _noteBox.Text.Length + " / 200";
            _noteOverlay.Visibility = Visibility.Visible;
            _noteBox.Focus();
            _noteBox.CaretIndex = _noteBox.Text.Length;
        }

        public void CloseNoteEditor(bool save)
        {
            if (_noteOverlay.Visibility != Visibility.Visible) return;
            if (!save)
            {
                _noteBox.Text = _noteBeforeEdit;
                if (NoteChanged != null) NoteChanged(_noteBeforeEdit);
            }
            _noteDraft = _noteBox.Text;
            _noteOverlay.Visibility = Visibility.Collapsed;
        }

        public bool IsNoteEditorOpen { get { return _noteOverlay.Visibility == Visibility.Visible; } }

        public void SetNoteDraft(string note)
        {
            _noteDraft = note ?? string.Empty;
            _noteLabel.Text = string.IsNullOrEmpty(_noteDraft)
                ? "  添加本次备注"
                : "  " + Truncate(_noteDraft, 18);
        }

        public void BuildHistory(HistoryData history)
        {
            _historyList.Children.Clear();
            if (history == null || history.IsEmpty())
            {
                _historySummaryText.Text = "还没有记录，完成一次专注后会出现在这里。";
                _exportDetailButton.IsEnabled = false;
                _exportSummaryButton.IsEnabled = false;
                var empty = new TextBlock
                {
                    Text = "暂无历史记录",
                    FontSize = 13,
                    Foreground = (Brush)FindResource("MutedBrush"),
                    FontFamily = new FontFamily("Microsoft YaHei UI"),
                    Margin = new Thickness(0, 40, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                _historyList.Children.Add(empty);
                return;
            }

            AllTimeSummary all = AllTimeSummary.FromHistory(history);
            _historySummaryText.Text = "累计 " + all.DaysWithData + " 天 · " + all.FocusCompleted +
                " 轮 · " + all.FocusMinutes + " 分钟 · 护眼 " + all.EyeRests + " 次";
            _exportDetailButton.IsEnabled = true;
            _exportSummaryButton.IsEnabled = true;

            List<DayRecord> days = history.DaysDescending();
            for (int i = 0; i < days.Count; i++)
            {
                AddDayRow(days[i], i == 0);
            }
        }

        private void AddDayRow(DayRecord day, bool expand)
        {
            var wrap = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

            string dayLabel = TimeFormat.DayLabel(day.Date);
            string detailLine = "完成 " + day.FocusCompleted + " 轮 · " + day.FocusMinutes +
                                " 分钟 · 护眼 " + day.EyeRests + " 次";

            var headerBtn = new Button
            {
                Style = (Style)FindResource("DayRowStyle"),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftStack = new StackPanel();
            leftStack.Children.Add(new TextBlock
            {
                Text = dayLabel,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextBrush"),
                FontFamily = new FontFamily("Microsoft YaHei UI")
            });
            leftStack.Children.Add(new TextBlock
            {
                Text = detailLine,
                FontSize = 12,
                Foreground = (Brush)FindResource("MutedBrush"),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                Margin = new Thickness(0, 2, 0, 0)
            });
            headerGrid.Children.Add(leftStack);
            Grid.SetColumn(leftStack, 0);

            var chevron = new TextBlock
            {
                Text = expand ? "\uE70D" : "\uE76C",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 11,
                Foreground = (Brush)FindResource("MutedBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            headerGrid.Children.Add(chevron);
            Grid.SetColumn(chevron, 1);

            headerBtn.Content = headerGrid;

            var detail = new StackPanel { Margin = new Thickness(12, 8, 0, 0), Visibility = expand ? Visibility.Visible : Visibility.Collapsed };
            BuildSessionRows(detail, day);

            headerBtn.Click += delegate { detail.Visibility = detail.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; };

            wrap.Children.Add(headerBtn);
            wrap.Children.Add(detail);
            _historyList.Children.Add(wrap);
        }

        private void BuildSessionRows(StackPanel detail, DayRecord day)
        {
            detail.Children.Clear();
            if (day.Sessions == null || day.Sessions.Count == 0)
            {
                detail.Children.Add(new TextBlock
                {
                    Text = "当天没有专注记录",
                    FontSize = 12,
                    Foreground = (Brush)FindResource("MutedBrush"),
                    FontFamily = new FontFamily("Microsoft YaHei UI")
                });
                return;
            }

            for (int i = day.Sessions.Count - 1; i >= 0; i--)
            {
                FocusSessionRecord s = day.Sessions[i];
                if (s == null) continue;
                DateTime start = HistoryData.FromIsoUtc(s.StartUtc, DateTime.Now);
                DateTime end = HistoryData.FromIsoUtc(s.EndUtc, DateTime.Now);

                var row = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
                var line = new TextBlock
                {
                    FontSize = 12,
                    Foreground = (Brush)FindResource("TextBrush"),
                    FontFamily = new FontFamily("Microsoft YaHei UI"),
                    TextWrapping = TextWrapping.Wrap
                };
                line.Text = start.ToString("HH:mm") + "–" + end.ToString("HH:mm") + "  " +
                            TimeFormat.DurationText(s.ActualSeconds) + "  ·  " +
                            TimeFormat.StatusLabel(s.Status);
                row.Children.Add(line);

                if (!string.IsNullOrEmpty(s.Note))
                {
                    row.Children.Add(new TextBlock
                    {
                        Text = "备注：" + s.Note,
                        FontSize = 12,
                        Foreground = (Brush)FindResource("MutedBrush"),
                        FontFamily = new FontFamily("Microsoft YaHei UI"),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 3, 0, 0)
                    });
                }
                detail.Children.Add(row);
            }
        }

        public void BuildSettings(AppSettings settings)
        {
            _settingsStack.Children.Clear();
            _settingsEdit = settings ?? AppSettings.CreateDefaults();

            var hint = new TextBlock
            {
                Text = "点击数字后直接输入，回车确认",
                FontSize = 11,
                Foreground = (Brush)FindResource("MutedBrush"),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            };
            _settingsStack.Children.Add(hint);

            AddSectionTitle("番茄钟");
            AddStepper("专注时长", _settingsEdit.FocusMinutes, 1, 120, "分钟", delegate(int v) { _settingsEdit.FocusMinutes = v; RaiseSettingsChanged(); });
            AddStepper("短休时长", _settingsEdit.ShortBreakMinutes, 1, 60, "分钟", delegate(int v) { _settingsEdit.ShortBreakMinutes = v; RaiseSettingsChanged(); });
            AddStepper("长休时长", _settingsEdit.LongBreakMinutes, 1, 60, "分钟", delegate(int v) { _settingsEdit.LongBreakMinutes = v; RaiseSettingsChanged(); });
            AddStepper("长休间隔", _settingsEdit.LongBreakEvery, 2, 12, "轮", delegate(int v) { _settingsEdit.LongBreakEvery = v; RaiseSettingsChanged(); });
            AddToggle("专注结束自动休息", _settingsEdit.AutoStartBreaks, delegate(bool v) { _settingsEdit.AutoStartBreaks = v; RaiseSettingsChanged(); });
            AddToggle("休息结束自动下一轮", _settingsEdit.AutoStartFocus, delegate(bool v) { _settingsEdit.AutoStartFocus = v; RaiseSettingsChanged(); });

            AddSectionTitle("护眼提醒");
            AddStepper("护眼间隔", _settingsEdit.EyeIntervalMinutes, 1, 180, "分钟", delegate(int v) { _settingsEdit.EyeIntervalMinutes = v; RaiseSettingsChanged(); });
            AddStepper("休息时长", _settingsEdit.EyeRestSeconds, 5, 120, "秒", delegate(int v) { _settingsEdit.EyeRestSeconds = v; RaiseSettingsChanged(); });
            AddToggle("全屏休息遮罩", _settingsEdit.FullscreenRest, delegate(bool v) { _settingsEdit.FullscreenRest = v; RaiseSettingsChanged(); });
            AddToggle("严格护眼（不可跳过）", _settingsEdit.StrictEyeRest, delegate(bool v) { _settingsEdit.StrictEyeRest = v; RaiseSettingsChanged(); });

            AddSectionTitle("背景音乐");
            AddMusicSection();

            AddSectionTitle("通用");
            AddToggle("提示音", _settingsEdit.SoundEnabled, delegate(bool v) { _settingsEdit.SoundEnabled = v; RaiseSettingsChanged(); });
            AddToggle("开机自启动", _settingsEdit.StartWithWindows, delegate(bool v) { _settingsEdit.StartWithWindows = v; RaiseSettingsChanged(); });
            AddToggle("窗口置顶", _settingsEdit.AlwaysOnTop, delegate(bool v) { _settingsEdit.AlwaysOnTop = v; RaiseSettingsChanged(); });
        }

        private void AddSectionTitle(string title)
        {
            var t = new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("MutedBrush"),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                Margin = new Thickness(0, 14, 0, 6)
            };
            _settingsStack.Children.Add(t);
        }

        private void AddStepper(string label, int value, int min, int max, string unit, Action<int> onChanged)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 13,
                Foreground = (Brush)FindResource("TextBrush"),
                FontFamily = new FontFamily("Microsoft YaHei UI")
            });
            row.Children.Add(left);
            Grid.SetColumn(left, 0);

            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            int current = value;
            var valueBox = new TextBox
            {
                Text = value.ToString(),
                Style = (Style)FindResource("TextBoxStyle"),
                Width = 56,
                Padding = new Thickness(4, 2, 4, 2),
                FontSize = 13,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "点击后直接输入，回车确认"
            };

            Action commit = delegate
            {
                int parsed;
                if (int.TryParse(valueBox.Text.Trim(), out parsed))
                {
                    parsed = Math.Max(min, Math.Min(max, parsed));
                    current = parsed;
                    valueBox.Text = parsed.ToString();
                    onChanged(parsed);
                }
                else
                {
                    valueBox.Text = current.ToString();
                }
            };
            valueBox.LostFocus += delegate { commit(); };
            valueBox.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Enter)
                {
                    commit();
                    valueBox.CaretIndex = valueBox.Text.Length;
                    e.Handled = true;
                }
            };

            var unitText = new TextBlock
            {
                Text = unit,
                FontSize = 12,
                Foreground = (Brush)FindResource("MutedBrush"),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };

            right.Children.Add(valueBox);
            right.Children.Add(unitText);
            row.Children.Add(right);
            Grid.SetColumn(right, 1);
            _settingsStack.Children.Add(row);
        }

        private void AddToggle(string label, bool value, Action<bool> onChanged)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var t = new TextBlock
            {
                Text = label,
                FontSize = 13,
                Foreground = (Brush)FindResource("TextBrush"),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                VerticalAlignment = VerticalAlignment.Center
            };
            row.Children.Add(t);
            Grid.SetColumn(t, 0);

            var toggle = new ToggleButton
            {
                Style = (Style)FindResource("SwitchStyle"),
                IsChecked = value,
                VerticalAlignment = VerticalAlignment.Center
            };
            toggle.Checked += delegate { onChanged(true); };
            toggle.Unchecked += delegate { onChanged(false); };
            row.Children.Add(toggle);
            Grid.SetColumn(toggle, 1);
            _settingsStack.Children.Add(row);
        }

        private void AddMusicSection()
        {
            var hint = new TextBlock
            {
                Text = "专注开始（含继续）时，若音乐窗口未打开则打开全部启用链接。",
                FontSize = 11,
                Foreground = (Brush)FindResource("MutedBrush"),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            _settingsStack.Children.Add(hint);

            if (_settingsEdit.MusicLinks == null) _settingsEdit.MusicLinks = new List<MusicLink>();
            for (int i = 0; i < _settingsEdit.MusicLinks.Count; i++)
            {
                MusicLink link = _settingsEdit.MusicLinks[i];
                AddMusicRow(link);
            }

            var addBtn = new Button
            {
                Content = "+ 添加音乐链接",
                Style = (Style)FindResource("SecondaryButtonStyle"),
                FontSize = 12,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 0)
            };
            addBtn.Click += delegate
            {
                if (_settingsEdit.MusicLinks.Count >= AppSettings.MaxMusicLinks) return;
                var link = new MusicLink { Label = "背景音乐 " + (_settingsEdit.MusicLinks.Count + 1) };
                _settingsEdit.MusicLinks.Add(link);
                BuildSettings(_settingsEdit.Clone());
                RaiseSettingsChanged();
            };
            _settingsStack.Children.Add(addBtn);
        }

        private void AddMusicRow(MusicLink link)
        {
            var border = new Border
            {
                Background = (Brush)FindResource("SubtleBrush"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var stack = new StackPanel();

            var labelBox = new TextBox
            {
                Text = link.Label ?? string.Empty,
                Style = (Style)FindResource("TextBoxStyle"),
                Margin = new Thickness(0, 0, 0, 6),
                ToolTip = "名称（可选）"
            };
            labelBox.LostFocus += delegate
            {
                link.Label = labelBox.Text.Trim();
                RaiseSettingsChanged();
            };

            var urlBox = new TextBox
            {
                Text = link.Url ?? string.Empty,
                Style = (Style)FindResource("TextBoxStyle"),
                Margin = new Thickness(0, 0, 0, 6),
                ToolTip = "网页地址，http/https"
            };
            urlBox.LostFocus += delegate
            {
                string url = urlBox.Text.Trim();
                if (url.Length > 0 && !AppSettings.IsValidUrl(url))
                {
                    urlBox.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 107, 87));
                }
                else
                {
                    urlBox.BorderBrush = (Brush)FindResource("CardBorderBrush");
                    link.Url = url;
                    if (link.Label.Length == 0 && url.Length > 0) link.Label = url;
                }
                RaiseSettingsChanged();
            };

            var controls = new StackPanel { Orientation = Orientation.Horizontal };
            var enable = new ToggleButton { Style = (Style)FindResource("SwitchStyle"), IsChecked = link.Enabled, Margin = new Thickness(0, 0, 10, 0) };
            enable.Checked += delegate { link.Enabled = true; RaiseSettingsChanged(); };
            enable.Unchecked += delegate { link.Enabled = false; RaiseSettingsChanged(); };

            var test = new Button
            {
                Content = "测试",
                Style = (Style)FindResource("SecondaryButtonStyle"),
                FontSize = 11,
                Height = 24,
                Margin = new Thickness(0, 0, 8, 0)
            };
            test.Click += delegate { if (MusicTestRequested != null) MusicTestRequested(link); };

            var delete = new Button
            {
                Content = "删除",
                Style = (Style)FindResource("SecondaryButtonStyle"),
                FontSize = 11,
                Height = 24
            };
            delete.Click += delegate
            {
                if (_settingsEdit.MusicLinks.Contains(link)) _settingsEdit.MusicLinks.Remove(link);
                BuildSettings(_settingsEdit.Clone());
                RaiseSettingsChanged();
            };

            controls.Children.Add(enable);
            controls.Children.Add(test);
            controls.Children.Add(delete);

            stack.Children.Add(labelBox);
            stack.Children.Add(urlBox);
            stack.Children.Add(controls);
            border.Child = stack;
            _settingsStack.Children.Add(border);
        }

        private void RaiseSettingsChanged()
        {
            if (SettingsChanged != null) SettingsChanged(_settingsEdit.Clone());
        }

        public AppSettings EditedSettings { get { return _settingsEdit; } }

        public void RequestClose()
        {
            _allowClose = true;
            Close();
        }

        public void BringToFront()
        {
            if (!IsVisible) Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            EnforceTopmost();
        }

        public void SetInteractive(bool enabled)
        {
            IsEnabled = enabled;
        }

        public void SetAlwaysOnTop(bool on)
        {
            _alwaysOnTop = on;
            Topmost = on;
            if (on) EnforceTopmost();

            Brush active = (Brush)FindResource("TomatoBrush");
            Brush muted = (Brush)FindResource("MutedBrush");
            _pinButton.Foreground = on ? active : muted;
            _pinButton.ToolTip = on ? "窗口置顶：开" : "窗口置顶：关";
        }

        private static double Clamp01(double v)
        {
            if (v < 0) return 0;
            if (v > 1) return 1;
            return v;
        }

        private static string Truncate(string s, int max)
        {
            if (s == null) return string.Empty;
            if (s.Length <= max) return s;
            return s.Substring(0, max) + "…";
        }

        private static Geometry BuildArc(double size, double thickness, double progress)
        {
            var geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                double center = size / 2.0;
                double radius = (size - thickness) / 2.0;
                double startAngle = -Math.PI / 2.0;

                if (progress <= 0)
                {
                    // empty
                }
                else if (progress >= 0.999)
                {
                    Point p0 = PointOnCircle(center, radius, startAngle);
                    Point mid = PointOnCircle(center, radius, startAngle + Math.PI);
                    ctx.BeginFigure(p0, false, false);
                    ctx.ArcTo(mid, new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
                    ctx.ArcTo(p0, new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
                }
                else
                {
                    double sweep = progress * Math.PI * 2.0;
                    double endAngle = startAngle + sweep;
                    Point start = PointOnCircle(center, radius, startAngle);
                    Point end = PointOnCircle(center, radius, endAngle);
                    bool largeArc = sweep > Math.PI;
                    ctx.BeginFigure(start, false, false);
                    ctx.ArcTo(end, new Size(radius, radius), 0, largeArc, SweepDirection.Clockwise, true, false);
                }
            }
            geometry.Freeze();
            return geometry;
        }

        private static Point PointOnCircle(double center, double radius, double angle)
        {
            return new Point(center + radius * Math.Cos(angle), center + radius * Math.Sin(angle));
        }
    }
}
