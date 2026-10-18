using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using PomoRest.Core;

namespace PomoRest.Services
{
    public sealed class MusicLauncher
    {
        private readonly object _gate = new object();
        private readonly HashSet<IntPtr> _managedWindows = new HashSet<IntPtr>();
        private bool _untracked;
        private bool _launching;

        private static readonly HashSet<string> BrowserProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "chrome", "msedge", "firefox", "brave", "vivaldi", "opera", "iexplore", "chromium"
        };

        public void Invalidate()
        {
            lock (_gate)
            {
                _managedWindows.Clear();
                _untracked = false;
            }
        }

        public bool HasLiveWindow()
        {
            lock (_gate)
            {
                var live = new List<IntPtr>();
                foreach (IntPtr h in _managedWindows)
                {
                    if (NativeMethods.IsWindow(h)) live.Add(h);
                    else live.Add(IntPtr.Zero);
                }
                _managedWindows.Clear();
                bool any = false;
                foreach (IntPtr h in live)
                {
                    if (h != IntPtr.Zero)
                    {
                        _managedWindows.Add(h);
                        any = true;
                    }
                }
                return any;
            }
        }

        public void EnsureOpenAsync(IList<MusicLink> links, Action<string> info, Action<string> error)
        {
            List<MusicLink> enabled = new List<MusicLink>();
            if (links != null)
                foreach (MusicLink l in links)
                    if (l != null && l.Enabled && AppSettings.IsValidUrl(l.Url))
                        enabled.Add(l);

            if (enabled.Count == 0) return;

            lock (_gate)
            {
                if (_launching) return;
                if (HasLiveWindow()) return;
                if (_untracked)
                {
                    // We could not reliably track the window earlier; avoid opening
                    // duplicates until the user explicitly asks again.
                    return;
                }
                _launching = true;
            }

            Task.Factory.StartNew(delegate()
            {
                try
                {
                    Launch(enabled, info, error);
                }
                finally
                {
                    lock (_gate) { _launching = false; }
                }
            });
        }

        public void ForceOpenAsync(IList<MusicLink> links, Action<string> info, Action<string> error)
        {
            List<MusicLink> enabled = new List<MusicLink>();
            if (links != null)
                foreach (MusicLink l in links)
                    if (l != null && l.Enabled && AppSettings.IsValidUrl(l.Url))
                        enabled.Add(l);
            if (enabled.Count == 0)
            {
                if (info != null) info("还没有配置有效的背景音乐链接");
                return;
            }

            lock (_gate)
            {
                if (_launching) return;
                _managedWindows.Clear();
                _untracked = false;
                _launching = true;
            }

            Task.Factory.StartNew(delegate()
            {
                try
                {
                    Launch(enabled, info, error);
                }
                finally
                {
                    lock (_gate) { _launching = false; }
                }
            });
        }

        private void Launch(List<MusicLink> links, Action<string> info, Action<string> error)
        {
            string exe = ResolveDefaultBrowserExe();
            var before = new HashSet<IntPtr>(NativeMethods.EnumTopLevelWindows());

            if (exe == null)
            {
                // Unknown browser: just hand each URL to the shell. We cannot track
                // its window reliably, so treat it as untracked to avoid duplicates.
                foreach (MusicLink l in links)
                {
                    try { Process.Start(new ProcessStartInfo(l.Url) { UseShellExecute = true }); }
                    catch (Exception ex) { if (error != null) error("打开失败：" + ex.Message); }
                }
                lock (_gate) { _untracked = true; }
                if (info != null) info("已打开背景音乐网页");
                return;
            }

            string name = Path.GetFileNameWithoutExtension(exe);
            bool firefox = name.IndexOf("firefox", StringComparison.OrdinalIgnoreCase) >= 0;
            string flag = firefox ? "-new-window" : "--new-window";
            var args = new System.Text.StringBuilder(flag);
            foreach (MusicLink l in links)
                args.Append(' ').Append(Quote(l.Url));

            try
            {
                var psi = new ProcessStartInfo(exe, args.ToString())
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                if (error != null) error("无法启动浏览器：" + ex.Message);
                lock (_gate) { _untracked = true; }
                return;
            }

            var found = PollForNewWindows(before, 5000);
            lock (_gate)
            {
                if (found.Count > 0)
                {
                    _managedWindows.Clear();
                    foreach (IntPtr h in found) _managedWindows.Add(h);
                    _untracked = false;
                }
                else
                {
                    _untracked = true;
                }
            }

            if (found.Count > 0)
            {
                if (info != null) info("已打开背景音乐网页");
            }
            else
            {
                if (info != null) info("已尝试打开背景音乐网页（未能识别浏览器窗口）");
            }
        }

        private static List<IntPtr> PollForNewWindows(HashSet<IntPtr> before, int timeoutMs)
        {
            var result = new List<IntPtr>();
            var seen = new HashSet<IntPtr>();
            int waited = 0;
            while (waited < timeoutMs)
            {
                foreach (IntPtr h in NativeMethods.EnumTopLevelWindows())
                {
                    if (before.Contains(h)) continue;
                    if (!NativeMethods.IsWindowVisible(h)) continue;
                    uint pid;
                    NativeMethods.GetWindowThreadProcessId(h, out pid);
                    if (pid == 0) continue;
                    string pname = ProcessName(pid);
                    if (string.IsNullOrEmpty(pname) || !BrowserProcessNames.Contains(pname)) continue;
                    if (!seen.Contains(h))
                    {
                        seen.Add(h);
                        result.Add(h);
                    }
                }
                if (result.Count > 0) break;
                Thread.Sleep(200);
                waited += 200;
            }
            return result;
        }

        private static string ProcessName(uint pid)
        {
            try
            {
                using (var p = Process.GetProcessById((int)pid))
                {
                    return p.ProcessName;
                }
            }
            catch
            {
                return null;
            }
        }

        private static string ResolveDefaultBrowserExe()
        {
            try
            {
                string progId;
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice"))
                {
                    progId = key == null ? null : key.GetValue("ProgId") as string;
                }
                if (string.IsNullOrEmpty(progId)) return null;

                string command = null;
                using (var key = Registry.ClassesRoot.OpenSubKey(progId + @"\shell\open\command"))
                {
                    command = key == null ? null : key.GetValue(null) as string;
                }
                if (string.IsNullOrEmpty(command)) return null;
                return ParseExePath(command);
            }
            catch
            {
                return null;
            }
        }

        private static string ParseExePath(string command)
        {
            command = command.Trim();
            if (command.Length == 0) return null;
            if (command[0] == '"')
            {
                int end = command.IndexOf('"', 1);
                if (end < 0) return null;
                string path = command.Substring(1, end - 1);
                return File.Exists(path) ? path : null;
            }
            int space = command.IndexOf(' ');
            string first = space < 0 ? command : command.Substring(0, space);
            // Some commands are "chrome.exe" without a directory; try to resolve a
            // standard install location as a fallback.
            if (File.Exists(first)) return first;
            if (first.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                string name = Path.GetFileName(first);
                string[] candidates = new string[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", name),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", name),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", name),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", name),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Mozilla Firefox", name),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Mozilla Firefox", name)
                };
                foreach (string c in candidates)
                    if (File.Exists(c)) return c;
            }
            return null;
        }

        private static string Quote(string s)
        {
            return "\"" + s.Replace("\"", "\\\"") + "\"";
        }
    }
}
