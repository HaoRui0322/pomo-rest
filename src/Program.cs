using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using PomoRest.Services;

namespace PomoRest
{
    public static class Program
    {
        private static Mutex _mutex;

        [STAThread]
        public static void Main(string[] args)
        {
            bool created;
            _mutex = new Mutex(true, SingleInstance.MutexName, out created);
            if (!created)
            {
                SingleInstance.SignalExisting();
                return;
            }

            try
            {
                AppPaths.EnsureDirectory();
                NativeMethods.SetCurrentProcessExplicitAppUserModelID("PomoRest.TomatoRest.1");
            }
            catch { }

            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.DispatcherUnhandledException += OnDispatcherException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            var controller = new AppController(app);
            try
            {
                controller.Start();
            }
            catch (Exception ex)
            {
                LogError(ex);
                try { System.Windows.Forms.MessageBox.Show("启动失败：" + ex.Message, "番茄小憩"); }
                catch { }
                app.Shutdown();
                return;
            }

            app.Run();

            try { _mutex.ReleaseMutex(); } catch { }
            try { _mutex.Dispose(); } catch { }
        }

        private static void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogError(e.Exception);
            e.Handled = true;
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try { LogError(e.ExceptionObject as Exception); } catch { }
        }

        private static void LogError(Exception ex)
        {
            if (ex == null) return;
            try
            {
                AppPaths.EnsureDirectory();
                File.AppendAllText(AppPaths.ErrorLogFile,
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + ex + Environment.NewLine);
            }
            catch { }
        }
    }
}
