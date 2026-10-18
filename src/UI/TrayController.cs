using System;
using System.Drawing;
using System.Windows.Forms;
using PomoRest.Core;

namespace PomoRest.UI
{
    public sealed class TrayController : IDisposable
    {
        public event EventHandler ToggleVisibility;
        public event EventHandler StartPause;
        public event Action<TimerMode> ModeSelected;
        public event EventHandler ShowHistory;
        public event EventHandler OpenMusic;
        public event EventHandler ToggleCompact;
        public event Action<bool> StartupChanged;
        public event Action<bool> TopmostChanged;
        public event EventHandler ExitRequested;

        private NotifyIcon _icon;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _showHideItem;
        private readonly ToolStripMenuItem _startPauseItem;
        private readonly ToolStripMenuItem _pomodoroItem;
        private readonly ToolStripMenuItem _eyeItem;
        private readonly ToolStripMenuItem _compactItem;
        private readonly ToolStripMenuItem _startupItem;
        private readonly ToolStripMenuItem _topmostItem;

        public TrayController()
        {
            _menu = new ContextMenuStrip();

            _showHideItem = new ToolStripMenuItem("显示/隐藏");
            _showHideItem.Click += delegate { if (ToggleVisibility != null) ToggleVisibility(this, EventArgs.Empty); };
            _menu.Items.Add(_showHideItem);

            _startPauseItem = new ToolStripMenuItem("开始");
            _startPauseItem.Click += delegate { if (StartPause != null) StartPause(this, EventArgs.Empty); };
            _menu.Items.Add(_startPauseItem);

            _menu.Items.Add(new ToolStripSeparator());

            _pomodoroItem = new ToolStripMenuItem("番茄钟");
            _pomodoroItem.Click += delegate { if (ModeSelected != null) ModeSelected(TimerMode.Pomodoro); };
            _menu.Items.Add(_pomodoroItem);

            _eyeItem = new ToolStripMenuItem("护眼提醒");
            _eyeItem.Click += delegate { if (ModeSelected != null) ModeSelected(TimerMode.EyeCare); };
            _menu.Items.Add(_eyeItem);

            var historyItem = new ToolStripMenuItem("查看记录");
            historyItem.Click += delegate { if (ShowHistory != null) ShowHistory(this, EventArgs.Empty); };
            _menu.Items.Add(historyItem);

            var musicItem = new ToolStripMenuItem("打开背景音乐");
            musicItem.Click += delegate { if (OpenMusic != null) OpenMusic(this, EventArgs.Empty); };
            _menu.Items.Add(musicItem);

            _menu.Items.Add(new ToolStripSeparator());

            _compactItem = new ToolStripMenuItem("紧凑模式");
            _compactItem.Click += delegate { if (ToggleCompact != null) ToggleCompact(this, EventArgs.Empty); };
            _menu.Items.Add(_compactItem);

            _startupItem = new ToolStripMenuItem("开机自启动");
            _startupItem.Click += delegate
            {
                bool next = !_startupItem.Checked;
                if (StartupChanged != null) StartupChanged(next);
            };
            _menu.Items.Add(_startupItem);

            _topmostItem = new ToolStripMenuItem("窗口置顶");
            _topmostItem.Click += delegate
            {
                bool next = !_topmostItem.Checked;
                if (TopmostChanged != null) TopmostChanged(next);
            };
            _menu.Items.Add(_topmostItem);

            _menu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate { if (ExitRequested != null) ExitRequested(this, EventArgs.Empty); };
            _menu.Items.Add(exitItem);

            CreateIcon();
        }

        private void CreateIcon()
        {
            _icon = new NotifyIcon();
            _icon.Icon = UiResources.LoadTrayIcon();
            _icon.Visible = true;
            _icon.Text = "番茄小憩";
            _icon.ContextMenuStrip = _menu;
            _icon.DoubleClick += delegate { if (ToggleVisibility != null) ToggleVisibility(this, EventArgs.Empty); };
        }

        public void Refresh()
        {
            try
            {
                _icon.Visible = false;
                _icon.Dispose();
            }
            catch { }
            CreateIcon();
        }

        public void SetVisible(bool visible)
        {
            _showHideItem.Text = visible ? "隐藏" : "显示";
        }

        public void SetRunning(bool running)
        {
            _startPauseItem.Text = running ? "暂停" : "开始";
        }

        public void SetMode(TimerMode mode)
        {
            _pomodoroItem.Checked = mode == TimerMode.Pomodoro;
            _eyeItem.Checked = mode == TimerMode.EyeCare;
        }

        public void SetCompact(bool compact)
        {
            _compactItem.Checked = compact;
        }

        public void SetStartup(bool enabled)
        {
            _startupItem.Checked = enabled;
        }

        public void SetTopmost(bool enabled)
        {
            _topmostItem.Checked = enabled;
        }

        public void SetTooltip(string text)
        {
            if (text == null) text = "番茄小憩";
            if (text.Length > 63) text = text.Substring(0, 63);
            _icon.Text = text;
        }

        public void ShowBalloon(string title, string message)
        {
            try
            {
                _icon.BalloonTipTitle = title;
                _icon.BalloonTipText = message;
                _icon.ShowBalloonTip(3000);
            }
            catch
            {
                // Notifications are best-effort.
            }
        }

        public void Dispose()
        {
            try
            {
                _icon.Visible = false;
                _icon.Dispose();
            }
            catch { }
        }
    }
}
