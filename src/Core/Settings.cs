using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PomoRest.Core
{
    [DataContract]
    public sealed class MusicLink
    {
        [DataMember(Name = "id")]
        public string Id;

        [DataMember(Name = "label")]
        public string Label;

        [DataMember(Name = "url")]
        public string Url;

        [DataMember(Name = "enabled")]
        public bool Enabled;

        public MusicLink()
        {
            Id = Guid.NewGuid().ToString("N");
            Label = string.Empty;
            Url = string.Empty;
            Enabled = true;
        }

        public MusicLink Clone()
        {
            return new MusicLink
            {
                Id = Id,
                Label = Label,
                Url = Url,
                Enabled = Enabled
            };
        }
    }

    [DataContract]
    public sealed class AppSettings
    {
        [DataMember(Name = "focusMinutes")]
        public int FocusMinutes = 25;
        [DataMember(Name = "shortBreakMinutes")]
        public int ShortBreakMinutes = 5;
        [DataMember(Name = "longBreakMinutes")]
        public int LongBreakMinutes = 15;
        [DataMember(Name = "longBreakEvery")]
        public int LongBreakEvery = 4;

        [DataMember(Name = "eyeIntervalMinutes")]
        public int EyeIntervalMinutes = 20;
        [DataMember(Name = "eyeRestSeconds")]
        public int EyeRestSeconds = 20;

        [DataMember(Name = "autoStartBreaks")]
        public bool AutoStartBreaks = true;
        [DataMember(Name = "autoStartFocus")]
        public bool AutoStartFocus = false;

        [DataMember(Name = "fullscreenRest")]
        public bool FullscreenRest = true;
        [DataMember(Name = "strictEyeRest")]
        public bool StrictEyeRest = false;

        [DataMember(Name = "soundEnabled")]
        public bool SoundEnabled = true;
        [DataMember(Name = "startWithWindows")]
        public bool StartWithWindows = true;
        [DataMember(Name = "alwaysOnTop")]
        public bool AlwaysOnTop = true;

        [DataMember(Name = "musicLinks")]
        public List<MusicLink> MusicLinks = new List<MusicLink>();

        public const int MaxMusicLinks = 6;

        public static int ClampInt(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public void Normalize()
        {
            FocusMinutes = ClampInt(FocusMinutes, 1, 120);
            ShortBreakMinutes = ClampInt(ShortBreakMinutes, 1, 60);
            LongBreakMinutes = ClampInt(LongBreakMinutes, 1, 60);
            LongBreakEvery = ClampInt(LongBreakEvery, 2, 12);
            EyeIntervalMinutes = ClampInt(EyeIntervalMinutes, 1, 180);
            EyeRestSeconds = ClampInt(EyeRestSeconds, 5, 120);

            if (MusicLinks == null) MusicLinks = new List<MusicLink>();
            // Keep at most MaxMusicLinks, dropping invalid and duplicate entries.
            var clean = new List<MusicLink>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (MusicLink link in MusicLinks)
            {
                if (link == null) continue;
                link.Label = (link.Label ?? string.Empty).Trim();
                link.Url = (link.Url ?? string.Empty).Trim();
                if (link.Url.Length == 0) continue;
                if (!IsValidUrl(link.Url)) continue;
                if (clean.Count >= MaxMusicLinks) break;
                if (seen.Contains(link.Url)) continue;
                seen.Add(link.Url);
                if (link.Id == null || link.Id.Length == 0) link.Id = Guid.NewGuid().ToString("N");
                if (link.Label.Length == 0) link.Label = "背景音乐 " + (clean.Count + 1);
                clean.Add(link);
            }
            MusicLinks = clean;
        }

        public static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            url = url.Trim();
            if (url.Length > 2048) return false;
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) return false;
            return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        public AppSettings Clone()
        {
            var c = new AppSettings();
            c.FocusMinutes = FocusMinutes;
            c.ShortBreakMinutes = ShortBreakMinutes;
            c.LongBreakMinutes = LongBreakMinutes;
            c.LongBreakEvery = LongBreakEvery;
            c.EyeIntervalMinutes = EyeIntervalMinutes;
            c.EyeRestSeconds = EyeRestSeconds;
            c.AutoStartBreaks = AutoStartBreaks;
            c.AutoStartFocus = AutoStartFocus;
            c.FullscreenRest = FullscreenRest;
            c.StrictEyeRest = StrictEyeRest;
            c.SoundEnabled = SoundEnabled;
            c.StartWithWindows = StartWithWindows;
            c.AlwaysOnTop = AlwaysOnTop;
            c.MusicLinks = new List<MusicLink>();
            foreach (MusicLink l in MusicLinks) c.MusicLinks.Add(l.Clone());
            return c;
        }

        public static AppSettings CreateDefaults()
        {
            var s = new AppSettings();
            s.Normalize();
            return s;
        }
    }
}
