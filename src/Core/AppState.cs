using System.Runtime.Serialization;

namespace PomoRest.Core
{
    [DataContract]
    public sealed class WindowStateData
    {
        [DataMember(Name = "left")]
        public double Left;
        [DataMember(Name = "top")]
        public double Top;
        [DataMember(Name = "compact")]
        public bool Compact;
    }

    [DataContract]
    public sealed class SessionStateData
    {
        [DataMember(Name = "mode")]
        public string Mode = "pomodoro";
        [DataMember(Name = "phase")]
        public string Phase = "focus";
        [DataMember(Name = "remainingSeconds")]
        public int RemainingSeconds = 1500;
        [DataMember(Name = "completedInCycle")]
        public int CompletedInCycle = 0;
        [DataMember(Name = "noteDraft")]
        public string NoteDraft = "";
        [DataMember(Name = "attemptStartedAtUtc")]
        public string AttemptStartedAtUtc = null;
    }

    [DataContract]
    public sealed class AppState
    {
        [DataMember(Name = "version")]
        public int Version = 1;

        [DataMember(Name = "settings")]
        public AppSettings Settings = new AppSettings();

        [DataMember(Name = "window")]
        public WindowStateData Window = new WindowStateData();

        [DataMember(Name = "session")]
        public SessionStateData Session = new SessionStateData();

        public static AppState CreateDefault()
        {
            var s = new AppState();
            s.Settings = AppSettings.CreateDefaults();
            s.Window.Left = -100000.0;
            s.Window.Top = -100000.0;
            s.Session.Mode = "pomodoro";
            s.Session.Phase = "focus";
            s.Session.RemainingSeconds = s.Settings.FocusMinutes * 60;
            return s;
        }
    }
}
