using System;
using System.Threading.Tasks;

namespace NServiceBus.Backplane
{
    interface IQuerySchedule
    {
        IDisposable Schedule(Func<Task> recurringAction);
    }
}