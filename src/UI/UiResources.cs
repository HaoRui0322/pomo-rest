using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media.Imaging;

namespace PomoRest.UI
{
    public static class UiResources
    {
        public static void MergeTheme()
        {
            var dict = (ResourceDictionary)LoadXaml("Theme.xaml");
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }

        public static object LoadXaml(string resourceName)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException("Missing embedded resource: " + resourceName);
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string xaml = reader.ReadToEnd();
                    return XamlReader.Parse(xaml);
                }
            }
        }

        public static Icon LoadTrayIcon()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream stream = asm.GetManifestResourceStream("app.ico"))
            {
                if (stream == null) return SystemIcons.Application;
                return new Icon(stream);
            }
        }

        public static BitmapFrame LoadWindowIconFrame()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream stream = asm.GetManifestResourceStream("app.ico"))
            {
                if (stream == null) return null;
                return BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            }
        }
    }
}
