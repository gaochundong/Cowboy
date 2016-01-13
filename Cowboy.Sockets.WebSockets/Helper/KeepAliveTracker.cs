using System;
using System.Diagnostics;
using System.Threading;

namespace Cowboy.WebSockets
{
    internal abstract class KeepAliveTracker : IDisposable
    {
        public abstract void OnDataReceived();
        public abstract void OnDataSent();
        public abstract void Dispose();
        public abstract void StartTimer();
        public abstract void ResetTimer();
        public abstract bool ShouldSendKeepAlive();

        public static KeepAliveTracker Create(TimeSpan keepAliveInterval, TimerCallback keepAliveCallback)
        {
            if ((int)keepAliveInterval.TotalMilliseconds > 0)
            {
                return new DefaultKeepAliveTracker(keepAliveInterval, keepAliveCallback);
            }

            return new DisabledKeepAliveTracker();
        }

        private class DisabledKeepAliveTracker : KeepAliveTracker
        {
            public override void OnDataReceived()
            {
            }

            public override void OnDataSent()
            {
            }

            public override void ResetTimer()
            {
            }

            public override void StartTimer()
            {
            }

            public override bool ShouldSendKeepAlive()
            {
                return false;
            }

            public override void Dispose()
            {
            }
        }

        private class DefaultKeepAliveTracker : KeepAliveTracker
        {
            private readonly TimerCallback _keepAliveTimerElapsedCallback;
            private readonly TimeSpan _keepAliveInterval;
            private readonly Stopwatch _lastSendActivity;
            private readonly Stopwatch _lastReceiveActivity;
            private Timer _keepAliveTimer;

            public DefaultKeepAliveTracker(TimeSpan keepAliveInterval, TimerCallback keepAliveCallback)
            {
                _keepAliveInterval = keepAliveInterval;
                _keepAliveTimerElapsedCallback = keepAliveCallback;
                _lastSendActivity = new Stopwatch();
                _lastReceiveActivity = new Stopwatch();
            }

            public override void OnDataReceived()
            {
                _lastReceiveActivity.Restart();
            }

            public override void OnDataSent()
            {
                _lastSendActivity.Restart();
            }

            public override void ResetTimer()
            {
                ResetTimer((int)_keepAliveInterval.TotalMilliseconds);
            }

            public override void StartTimer()
            {
                int keepAliveIntervalMilliseconds = (int)_keepAliveInterval.TotalMilliseconds;

                if (ExecutionContext.IsFlowSuppressed())
                {
                    _keepAliveTimer = new Timer(_keepAliveTimerElapsedCallback, null, Timeout.Infinite, Timeout.Infinite);
                    _keepAliveTimer.Change(keepAliveIntervalMilliseconds, Timeout.Infinite);
                }
                else
                {
                    using (ExecutionContext.SuppressFlow())
                    {
                        _keepAliveTimer = new Timer(_keepAliveTimerElapsedCallback, null, Timeout.Infinite, Timeout.Infinite);
                        _keepAliveTimer.Change(keepAliveIntervalMilliseconds, Timeout.Infinite);
                    }
                }
            }

            public override bool ShouldSendKeepAlive()
            {
                TimeSpan idleTime = GetIdleTime();
                if (idleTime >= _keepAliveInterval)
                {
                    return true;
                }

                ResetTimer((int)(_keepAliveInterval - idleTime).TotalMilliseconds);
                return false;
            }

            public override void Dispose()
            {
                _keepAliveTimer.Dispose();
            }

            private void ResetTimer(int dueInMilliseconds)
            {
                _keepAliveTimer.Change(dueInMilliseconds, Timeout.Infinite);
            }

            private TimeSpan GetIdleTime()
            {
                TimeSpan sinceLastSendActivity = GetTimeElapsed(_lastSendActivity);
                TimeSpan sinceLastReceiveActivity = GetTimeElapsed(_lastReceiveActivity);

                if (sinceLastReceiveActivity < sinceLastSendActivity)
                {
                    return sinceLastReceiveActivity;
                }

                return sinceLastSendActivity;
            }

            private TimeSpan GetTimeElapsed(Stopwatch watch)
            {
                if (watch.IsRunning)
                {
                    return watch.Elapsed;
                }

                return _keepAliveInterval;
            }
        }
    }
}
