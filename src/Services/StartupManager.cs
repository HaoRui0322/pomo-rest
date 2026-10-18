using System;
using Microsoft.Win32;

namespace PomoRest.Services
{
    public sealed class StartupManager
    {
        private readonly string _runSubKey;
        private readonly string _valueName;

        public StartupManager(string runSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run",
                              string valueName = "PomoRest")
        {
            _runSubKey = runSubKey;
            _valueName = valueName;
        }

        public bool Enable(string exePath)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(_runSubKey))
                {
                    if (key == null) return false;
                    key.SetValue(_valueName, Quote(exePath), RegistryValueKind.String);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool Disable()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(_runSubKey, true))
                {
                    if (key == null) return true;
                    if (key.GetValue(_valueName) == null) return true;
                    key.DeleteValue(_valueName, false);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(_runSubKey, false))
                {
                    return key != null && key.GetValue(_valueName) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string Quote(string path)
        {
            return "\"" + (path ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }
    }
}
