using System;
using System.Threading;
using System.Threading.Tasks;

namespace NServiceBus.Backplane.Internal
{
    internal class DefaultQuerySchedule : IQuerySchedule
    {
        public IDisposable Schedule(Func<Task> recurringAction)
        {
            var timer = new TimerWrapper(recurringAction, TimeSpan.FromSeconds(5));
            return timer;
        }

        private class TimerWrapper : IDisposable
        {
            private readonly Timer _timer;

            public TimerWrapper(Func<Task> recurringAction, TimeSpan period)
            {
                _timer = new Timer(state => { recurringAction().ConfigureAwait(false).GetAwaiter().GetResult(); }, null, TimeSpan.Zero, period);
            }

            public void Dispose()
            {
                using (var waitHandle = new ManualResetEvent(false))
                {
                    _timer.Dispose(waitHandle);
                    waitHandle.WaitOne();
                }
            }
        }
    }
}