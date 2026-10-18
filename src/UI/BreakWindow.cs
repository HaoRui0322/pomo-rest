using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using PomoRest.Services;

namespace PomoRest.UI
{
    public partial class BreakWindow : Window
    {
        public event Action<bool> Completed; // true = skipped

        private TextBlock _countText;
        private Path _progressArc;
        private TextBlock _titleText;
        private TextBlock _subText;
        private TextBlock _hintText;
        private Button _skipButton;
        private TextBlock _skipHintText;
        private Grid _root;

        private int _durationSeconds;
        private int _remainingSeconds;
        private bool _strict;
        private bool _allowClose;
        private bool _finished;
        private DispatcherTimer _timer;
        private IntPtr _hwnd;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _endMs;

        public BreakWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AllowsTransparency = true;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            FontFamily = new FontFamily("Microsoft YaHei UI");

            Content = (UIElement)UiResources.LoadXaml("BreakWindow.xaml");
            _root = (Grid)Content;
            Icon = UiResources.LoadWindowIconFrame();

            _countText = (TextBlock)_root.FindName("CountText");
            _progressArc = (Path)_root.FindName("ProgressArc");
            _titleText = (TextBlock)_root.FindName("TitleText");
            _subText = (TextBlock)_root.FindName("SubText");
            _hintText = (TextBlock)_root.FindName("HintText");
            _skipButton = (Button)_root.FindName("SkipButton");
            _skipHintText = (TextBlock)_root.FindName("SkipHintText");

            _skipButton.Click += delegate { if (_skipButton.IsEnabled) Finish(true); };

            SourceInitialized += delegate { _hwnd = new WindowInteropHelper(this).Handle; };
            Deactivated += delegate { EnforceTopmost(); };
            Closing += OnClosing;
            PreviewKeyDown += OnPreviewKeyDown;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.Tick += OnTick;
        }

        public void Start(int seconds, bool strict, bool fullscreen)
        {
            _durationSeconds = Math.Max(5, seconds);
            _remainingSeconds = _durationSeconds;
            _endMs = _clock.ElapsedMilliseconds + _durationSeconds * 1000L;
            _strict = strict;
            _finished = false;
            _allowClose = false;

            _titleText.Text = "目光移向 6 米外";
            _subText.Text = "放松睫状肌，眨眨眼";
            _hintText.Text = "休息结束后自动继续护眼计时";

            if (strict)
            {
                _skipButton.Visibility = Visibility.Collapsed;
                _skipHintText.Text = "严格模式：本次休息不能跳过";
            }
            else
            {
                _skipButton.Visibility = Visibility.Visible;
                _skipButton.IsEnabled = false;
                _skipHintText.Text = "5 秒后可跳过";
            }

            if (fullscreen)
            {
                Width = SystemParameters.VirtualScreenWidth;
                Height = SystemParameters.VirtualScreenHeight;
                Left = SystemParameters.VirtualScreenLeft;
                Top = SystemParameters.VirtualScreenTop;
                _root.Background = new SolidColorBrush(Color.FromArgb(0xC4, 0x0A, 0x0C, 0x12));
            }
            else
            {
                Width = 560;
                Height = 380;
                Left = (SystemParameters.WorkArea.Left + SystemParameters.WorkArea.Width - Width) / 2.0;
                Top = (SystemParameters.WorkArea.Top + SystemParameters.WorkArea.Height - Height) / 2.0;
                _root.Background = Brushes.Transparent;
            }

            Render();
            Show();
            Activate();
            EnforceTopmost();
            _timer.Start();
        }

        private void OnTick(object sender, EventArgs e)
        {
            long remMs = _endMs - _clock.ElapsedMilliseconds;
            int remSec = (int)Math.Ceiling(remMs / 1000.0);
            if (remSec < 0) remSec = 0;
            _remainingSeconds = remSec;

            int elapsed = _durationSeconds - _remainingSeconds;
            if (!_strict && elapsed >= 5)
            {
                _skipButton.IsEnabled = true;
                _skipHintText.Text = "跳过休息";
            }

            Render();

            if (_remainingSeconds <= 0)
            {
                _timer.Stop();
                Finish(false);
            }
        }

        private void Render()
        {
            _countText.Text = _remainingSeconds.ToString();
            double progress = _remainingSeconds / (double)Math.Max(1, _durationSeconds);
            if (progress < 0) progress = 0;
            if (progress > 1) progress = 1;
            _progressArc.Data = BuildArc(150, 8, progress);
        }

        private void Finish(bool skipped)
        {
            if (_finished) return;
            _finished = true;
            _timer.Stop();
            Hide();
            var handler = Completed;
            if (handler != null) handler(skipped);
        }

        public void ForceFinishAndCredit()
        {
            // Called on system lock/suspend: the user is away from the screen, so
            // credit the rest and move on without re-showing the overlay.
            Finish(false);
        }

        public void ForceClose()
        {
            _finished = true;
            _timer.Stop();
            Hide();
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                if (!_strict)
                {
                    int elapsed = _durationSeconds - _remainingSeconds;
                    if (elapsed >= 5) Finish(true);
                }
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || (e.Key == Key.System && e.SystemKey == Key.F4))
            {
                e.Handled = true;
                if (_strict) return;
                int elapsed = _durationSeconds - _remainingSeconds;
                if (elapsed >= 5) Finish(true);
            }
        }

        public void EnforceTopmost()
        {
            if (_hwnd != IntPtr.Zero) NativeMethods.EnforceTopmost(_hwnd);
        }

        private static Geometry BuildArc(double size, double thickness, double progress)
        {
            var geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                double center = size / 2.0;
                double radius = (size - thickness) / 2.0;
                double startAngle = -Math.PI / 2.0;

                if (progress >= 0.999)
                {
                    Point p0 = PointOnCircle(center, radius, startAngle);
                    Point mid = PointOnCircle(center, radius, startAngle + Math.PI);
                    ctx.BeginFigure(p0, false, false);
                    ctx.ArcTo(mid, new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
                    ctx.ArcTo(p0, new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
                }
                else if (progress > 0)
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
