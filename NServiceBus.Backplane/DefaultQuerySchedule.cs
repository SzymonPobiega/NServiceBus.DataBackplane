using System;
using System.Threading;
using System.Threading.Tasks;

namespace NServiceBus.Backplane
{
    public class DefaultQuerySchedule : IQuerySchedule
    {
        public IDisposable Schedule(Func<Task> recurringAction)
        {
            var timer = new TimerWrapper(recurringAction, TimeSpan.FromSeconds(5));
            return timer;
        }

        class TimerWrapper : IDisposable
        {
            private readonly Timer timer;

            public TimerWrapper(Func<Task> recurringAction, TimeSpan period)
            {
                timer = new Timer(state =>
                {
                    recurringAction().ConfigureAwait(false).GetAwaiter().GetResult();
                }, null, TimeSpan.Zero, period);
            }

            public void Dispose()
            {
                using (var waitHandle = new ManualResetEvent(false))
                {
                    timer.Dispose(waitHandle);

                    // TODO: Use async synchronization primitive
                    waitHandle.WaitOne();
                }
            }
        }
    }
}