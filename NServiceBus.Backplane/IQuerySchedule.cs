using System;
using System.Threading.Tasks;

namespace NServiceBus.Backplane
{
    public interface IQuerySchedule
    {
        IDisposable Schedule(Func<Task> recurringAction);
    }
}