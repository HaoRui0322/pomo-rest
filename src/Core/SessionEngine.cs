using System;
using System.Collections.Generic;

namespace PomoRest.Core
{
    public sealed class SessionEngine
    {
        private const long LongGapMs = 15000;

        private readonly TimerMode _mode;
        private AppSettings _settings;
        private TimerPhase _phase;
        private double _remainingMs;
        private bool _running;
        private long _lastTickMs;
        private int _completedInCycle;
        private bool _eyeRestResume = true;

        public SessionEngine(TimerMode mode, AppSettings settings)
        {
            _mode = mode;
            _settings = settings;
            ResetToModeStart();
        }

        public TimerMode Mode { get { return _mode; } }
        public TimerPhase Phase { get { return _phase; } }
        public bool Running { get { return _running; } }
        public int CompletedInCycle { get { return _completedInCycle; } }

        public double RemainingMs { get { return _remainingMs; } }
        public double DurationMs { get { return PhaseDurationMs(_phase); } }

        public int RemainingSeconds
        {
            get { return (int)Math.Ceiling(_remainingMs / 1000.0); }
        }

        public void ResetToModeStart()
        {
            _phase = (_mode == TimerMode.Pomodoro) ? TimerPhase.Focus : TimerPhase.EyeWork;
            _remainingMs = PhaseDurationMs(_phase);
            _running = false;
            _completedInCycle = 0;
            _eyeRestResume = true;
        }

        public int PhaseDurationSeconds(TimerPhase phase)
        {
            switch (phase)
            {
                case TimerPhase.Focus: return _settings.FocusMinutes * 60;
                case TimerPhase.ShortBreak: return _settings.ShortBreakMinutes * 60;
                case TimerPhase.LongBreak: return _settings.LongBreakMinutes * 60;
                case TimerPhase.EyeWork: return _settings.EyeIntervalMinutes * 60;
                case TimerPhase.EyeRest: return _settings.EyeRestSeconds;
                default: return _settings.FocusMinutes * 60;
            }
        }

        private double PhaseDurationMs(TimerPhase phase)
        {
            return PhaseDurationSeconds(phase) * 1000.0;
        }

        public void ApplySettings(AppSettings settings, long nowMs)
        {
            double oldFull = PhaseDurationMs(_phase);
            _settings = settings;
            // If the current phase has not really started (paused at its full length),
            // adopt the new duration immediately; otherwise the change affects the next phase.
            if (!_running && _remainingMs >= oldFull - 500.0)
            {
                _remainingMs = PhaseDurationMs(_phase);
            }
        }

        public void RestoreState(TimerPhase phase, int remainingSeconds, int completedInCycle, long nowMs)
        {
            _completedInCycle = Math.Max(0, completedInCycle);
            _phase = phase;
            double full = PhaseDurationMs(_phase);
            double rem = remainingSeconds * 1000.0;
            if (rem <= 0 || rem > full) rem = full;
            _remainingMs = rem;
            _running = false;
            _eyeRestResume = true;
            _lastTickMs = nowMs;
        }

        public void Start(long nowMs)
        {
            if (_phase == TimerPhase.EyeRest) return; // handled by the overlay
            if (_remainingMs <= 0) _remainingMs = PhaseDurationMs(_phase);
            _running = true;
            _lastTickMs = nowMs;
        }

        public void Pause(long nowMs)
        {
            if (_running)
            {
                Tick(nowMs);
            }
            _running = false;
        }

        public void StopPause(long nowMs)
        {
            _running = false;
            _lastTickMs = nowMs;
        }

        public TimerTickResult Tick(long nowMs)
        {
            var result = new TimerTickResult();
            if (!_running) return result;

            long delta = nowMs - _lastTickMs;
            if (delta < 0)
            {
                _lastTickMs = nowMs;
                return result;
            }
            if (delta > LongGapMs)
            {
                // Treat an unexpectedly large gap as suspend/resume; pause rather than
                // silently completing a phase the user never saw running.
                _running = false;
                _lastTickMs = nowMs;
                result.LongGapAutoPaused = true;
                return result;
            }

            _remainingMs -= delta;
            _lastTickMs = nowMs;

            while (_remainingMs <= 0 && _running)
            {
                long overshoot = (long)(-_remainingMs);
                _lastTickMs = nowMs - Math.Min(overshoot, 1000);
                _remainingMs = 0;
                CompletePhase(nowMs, result);
            }
            return result;
        }

        private void CompletePhase(long nowMs, TimerTickResult result)
        {
            switch (_phase)
            {
                case TimerPhase.Focus:
                    {
                        int planned = PhaseDurationSeconds(_phase);
                        _completedInCycle++;
                        TimerPhase next = (_completedInCycle >= _settings.LongBreakEvery) ? TimerPhase.LongBreak : TimerPhase.ShortBreak;
                        _phase = next;
                        _remainingMs = PhaseDurationMs(next);
                        _running = _settings.AutoStartBreaks;
                        _lastTickMs = nowMs;
                        result.Events.Add(new TimerEvent(TimerEventType.FocusCompleted, TimerPhase.Focus, next, planned, planned, false));
                    }
                    break;

                case TimerPhase.ShortBreak:
                case TimerPhase.LongBreak:
                    {
                        TimerPhase from = _phase;
                        bool wasLong = (_phase == TimerPhase.LongBreak);
                        _phase = TimerPhase.Focus;
                        _remainingMs = PhaseDurationMs(_phase);
                        _running = _settings.AutoStartFocus;
                        _lastTickMs = nowMs;
                        if (wasLong) _completedInCycle = 0;
                        result.Events.Add(new TimerEvent(TimerEventType.BreakCompleted, from, TimerPhase.Focus,
                            PhaseDurationSeconds(from), PhaseDurationSeconds(from), false));
                        if (_running)
                            result.Events.Add(new TimerEvent(TimerEventType.FocusStarted, TimerPhase.Focus, TimerPhase.Focus,
                                PhaseDurationSeconds(_phase), PhaseDurationSeconds(_phase), false));
                    }
                    break;

                case TimerPhase.EyeWork:
                    {
                        int planned = PhaseDurationSeconds(_phase);
                        _phase = TimerPhase.EyeRest;
                        _remainingMs = PhaseDurationMs(_phase);
                        _eyeRestResume = true;
                        _running = false;
                        _lastTickMs = nowMs;
                        result.Events.Add(new TimerEvent(TimerEventType.EyeRestStarted, TimerPhase.EyeWork, TimerPhase.EyeRest,
                            planned, planned, false));
                    }
                    break;

                case TimerPhase.EyeRest:
                    // Should be completed via CompleteEyeRest, not by ticking.
                    _running = false;
                    break;
            }
        }

        public TimerEvent Skip(long nowMs)
        {
            bool wasRunning = _running;
            Pause(nowMs);
            switch (_phase)
            {
                case TimerPhase.Focus:
                    {
                        int planned = PhaseDurationSeconds(_phase);
                        int actual = PlannedMinusRemaining(planned);
                        _phase = TimerPhase.ShortBreak;
                        _remainingMs = PhaseDurationMs(_phase);
                        _running = _settings.AutoStartBreaks;
                        _lastTickMs = nowMs;
                        return new TimerEvent(TimerEventType.FocusSkipped, TimerPhase.Focus, _phase, planned, actual, true);
                    }

                case TimerPhase.ShortBreak:
                case TimerPhase.LongBreak:
                    {
                        bool wasLong = (_phase == TimerPhase.LongBreak);
                        _phase = TimerPhase.Focus;
                        _remainingMs = PhaseDurationMs(_phase);
                        _running = _settings.AutoStartFocus;
                        _lastTickMs = nowMs;
                        if (wasLong) _completedInCycle = 0;
                        if (_running)
                            return new TimerEvent(TimerEventType.FocusStarted, TimerPhase.Focus, TimerPhase.Focus,
                                PhaseDurationSeconds(_phase), PhaseDurationSeconds(_phase), false);
                        return null;
                    }

                case TimerPhase.EyeWork:
                    {
                        int planned = PhaseDurationSeconds(_phase);
                        _phase = TimerPhase.EyeRest;
                        _remainingMs = PhaseDurationMs(_phase);
                        _eyeRestResume = wasRunning;
                        _running = false;
                        _lastTickMs = nowMs;
                        return new TimerEvent(TimerEventType.EyeRestStarted, TimerPhase.EyeWork, TimerPhase.EyeRest, planned, planned, false);
                    }

                default:
                    return null;
            }
        }

        public TimerEvent Reset(long nowMs)
        {
            Pause(nowMs);
            switch (_phase)
            {
                case TimerPhase.Focus:
                    {
                        int planned = PhaseDurationSeconds(_phase);
                        int actual = PlannedMinusRemaining(planned);
                        _remainingMs = PhaseDurationMs(_phase);
                        _running = false;
                        return new TimerEvent(TimerEventType.FocusReset, TimerPhase.Focus, TimerPhase.Focus, planned, actual, false);
                    }
                case TimerPhase.ShortBreak:
                case TimerPhase.LongBreak:
                case TimerPhase.EyeWork:
                    _remainingMs = PhaseDurationMs(_phase);
                    _running = false;
                    return null;
                default:
                    return null;
            }
        }

        public void BeginEyeRestNow(long nowMs)
        {
            if (_phase != TimerPhase.EyeWork) return;
            int planned = PhaseDurationSeconds(_phase);
            _eyeRestResume = _running;
            _phase = TimerPhase.EyeRest;
            _remainingMs = PhaseDurationMs(_phase);
            _running = false;
            _lastTickMs = nowMs;
            // The controller observes the phase change directly and opens the overlay.
        }

        public TimerEvent CompleteEyeRest(long nowMs, bool skipped)
        {
            if (_phase != TimerPhase.EyeRest) return null;
            int planned = PhaseDurationSeconds(_phase);
            _phase = TimerPhase.EyeWork;
            _remainingMs = PhaseDurationMs(_phase);
            _running = _eyeRestResume;
            _eyeRestResume = true;
            _lastTickMs = nowMs;
            return new TimerEvent(TimerEventType.EyeRestCompleted, TimerPhase.EyeRest, TimerPhase.EyeWork,
                planned, planned, skipped);
        }

        public bool IsEyeRestActive()
        {
            return _phase == TimerPhase.EyeRest;
        }

        public SessionSnapshot GetSnapshot(int eyeRestsToday)
        {
            return new SessionSnapshot
            {
                Mode = _mode,
                Phase = _phase,
                RemainingMs = Math.Max(0, _remainingMs),
                DurationMs = Math.Max(1, PhaseDurationMs(_phase)),
                Running = _running,
                CompletedInCycle = _completedInCycle,
                EyeRestsToday = eyeRestsToday
            };
        }

        private int PlannedMinusRemaining(int planned)
        {
            double used = (planned * 1000.0) - _remainingMs;
            if (used < 0) used = 0;
            return (int)Math.Round(used / 1000.0);
        }
    }
}
