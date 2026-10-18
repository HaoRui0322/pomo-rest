using System;
using System.Collections.Generic;

namespace PomoRest.Core
{
    public enum TimerMode
    {
        Pomodoro = 0,
        EyeCare = 1
    }

    public enum TimerPhase
    {
        Focus = 0,
        ShortBreak = 1,
        LongBreak = 2,
        EyeWork = 3,
        EyeRest = 4
    }

    public enum TimerEventType
    {
        FocusCompleted = 1,
        FocusStarted = 2,
        FocusSkipped = 3,
        FocusReset = 4,
        BreakCompleted = 5,
        EyeRestStarted = 6,
        EyeRestCompleted = 7
    }

    public sealed class TimerEvent
    {
        public TimerEventType Type;
        public TimerPhase From;
        public TimerPhase To;
        public int PlannedSeconds;
        public int ActualSeconds;
        public bool Skipped;

        public TimerEvent(TimerEventType type, TimerPhase from, TimerPhase to, int plannedSeconds, int actualSeconds, bool skipped)
        {
            Type = type;
            From = from;
            To = to;
            PlannedSeconds = plannedSeconds;
            ActualSeconds = actualSeconds;
            Skipped = skipped;
        }
    }

    public sealed class TimerTickResult
    {
        public bool LongGapAutoPaused;
        public List<TimerEvent> Events = new List<TimerEvent>();
    }

    public sealed class SessionSnapshot
    {
        public TimerMode Mode;
        public TimerPhase Phase;
        public double RemainingMs;
        public double DurationMs;
        public bool Running;
        public int CompletedInCycle;
        public int EyeRestsToday;

        public int RemainingSeconds
        {
            get { return (int)Math.Ceiling(RemainingMs / 1000.0); }
        }

        public int DurationSeconds
        {
            get { return (int)Math.Round(DurationMs / 1000.0); }
        }
    }
}
