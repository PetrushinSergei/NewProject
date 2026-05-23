using System;
using System.Collections.Generic;
using System.Threading;

namespace PowerGridEditor
{
    public static class ParameterAutoChangeService
    {
        private sealed class AutoChangeState
        {
            public double Step;
            public int IntervalSeconds;
            public Timer Timer;
            public Func<double> Getter;
            public Action<double> Setter;
            public Action OnTick;
        }

        private static readonly Dictionary<string, AutoChangeState> states = new Dictionary<string, AutoChangeState>();
        private static readonly object sync = new object();

        public static string BuildId(object data, string key)
        {
            return $"{data.GetHashCode()}::{key}";
        }

        public static bool TryGet(string id, out double step, out int interval, out bool running)
        {
            lock (sync)
            {
                if (states.TryGetValue(id, out var state))
                {
                    step = state.Step;
                    interval = state.IntervalSeconds;
                    running = state.Timer != null;
                    return true;
                }
            }

            step = 1;
            interval = 1;
            running = false;
            return false;
        }


        public static void StopAll()
        {
            lock (sync)
            {
                foreach (var state in states.Values)
                {
                    state.Timer?.Dispose();
                    state.Timer = null;
                }
            }
        }

        public static void Configure(string id, double step, int intervalSeconds, bool enabled, Func<double> getter, Action<double> setter, Action onTick)
        {
            lock (sync)
            {
                if (!states.TryGetValue(id, out var state))
                {
                    state = new AutoChangeState();
                    states[id] = state;
                }

                state.Step = step;
                state.IntervalSeconds = intervalSeconds < 1 ? 1 : intervalSeconds;
                state.Getter = getter;
                state.Setter = setter;
                state.OnTick = onTick;

                state.Timer?.Dispose();
                state.Timer = null;

                if (enabled)
                {
                    int ms = state.IntervalSeconds * 1000;
                    state.Timer = new Timer(_ =>
                    {
                        try
                        {
                            double current = state.Getter();
                            state.Setter(current + state.Step);
                            state.OnTick?.Invoke();
                        }
                        catch { }
                    }, null, ms, ms);
                }
            }
        }
    }
}
