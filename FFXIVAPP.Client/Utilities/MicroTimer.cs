﻿// FFXIVAPP.Client
// MicroTimer.cs
// 
// © 2013 Ryan Wilson

using System.Threading;
using SmartAssembly.Attributes;

namespace FFXIVAPP.Client.Utilities
{
    [DoNotObfuscate]
    public class MicroTimer
    {
        public delegate void MicroTimerElapsedEventHandler(object sender, MicroTimerEventArgs timerEventArgs);

        private long _ignoreEventIfLateBy = long.MaxValue;
        private bool _stopTimer = true;
        private Thread _threadTimer;
        private long _timerIntervalInMicroSec;

        public MicroTimer()
        {
        }

        public MicroTimer(long timerIntervalInMicroseconds)
        {
            Interval = timerIntervalInMicroseconds;
        }

        public long Interval
        {
            get { return Interlocked.Read(ref _timerIntervalInMicroSec); }
            set { Interlocked.Exchange(ref _timerIntervalInMicroSec, value); }
        }

        public long IgnoreEventIfLateBy
        {
            get { return Interlocked.Read(ref _ignoreEventIfLateBy); }
            set { Interlocked.Exchange(ref _ignoreEventIfLateBy, value <= 0 ? long.MaxValue : value); }
        }

        public bool Enabled
        {
            set
            {
                if (value)
                {
                    Start();
                }
                else
                {
                    Stop();
                }
            }
            get { return (_threadTimer != null && _threadTimer.IsAlive); }
        }

        public event MicroTimerElapsedEventHandler MicroTimerElapsed;

        public void Start()
        {
            if (Enabled || Interval <= 0)
            {
                return;
            }

            _stopTimer = false;

            ThreadStart threadStart = () => NotificationTimer(ref _timerIntervalInMicroSec, ref _ignoreEventIfLateBy, ref _stopTimer);

            _threadTimer = new Thread(threadStart)
            {
                Priority = ThreadPriority.Highest
            };
            _threadTimer.Start();
        }

        public void Stop()
        {
            _stopTimer = true;
        }

        public void StopAndWait()
        {
            StopAndWait(Timeout.Infinite);
        }

        public bool StopAndWait(int timeoutInMilliSec)
        {
            _stopTimer = true;

            if (!Enabled || _threadTimer.ManagedThreadId == Thread.CurrentThread.ManagedThreadId)
            {
                return true;
            }

            return _threadTimer.Join(timeoutInMilliSec);
        }

        public void Abort()
        {
            _stopTimer = true;

            if (Enabled)
            {
                _threadTimer.Abort();
            }
        }

        private void NotificationTimer(ref long timerIntervalInMicroSec, ref long ignoreEventIfLateBy, ref bool stopTimer)
        {
            var timerCount = 0;
            long nextNotification = 0;

            var microStopwatch = new MicroStopwatch();
            microStopwatch.Start();

            while (!stopTimer)
            {
                var callbackFunctionExecutionTime = microStopwatch.ElapsedMicroseconds - nextNotification;

                var timerIntervalInMicroSecCurrent = Interlocked.Read(ref timerIntervalInMicroSec);
                var ignoreEventIfLateByCurrent = Interlocked.Read(ref ignoreEventIfLateBy);

                nextNotification += timerIntervalInMicroSecCurrent;
                timerCount++;
                long elapsedMicroseconds;

                while ((elapsedMicroseconds = microStopwatch.ElapsedMicroseconds) < nextNotification)
                {
                    Thread.SpinWait(10);
                }

                var timerLateBy = elapsedMicroseconds - nextNotification;

                if (timerLateBy >= ignoreEventIfLateByCurrent)
                {
                    continue;
                }

                var microTimerEventArgs = new MicroTimerEventArgs(timerCount, elapsedMicroseconds, timerLateBy, callbackFunctionExecutionTime);
                MicroTimerElapsed(this, microTimerEventArgs);
            }

            microStopwatch.Stop();
        }
    }
}
