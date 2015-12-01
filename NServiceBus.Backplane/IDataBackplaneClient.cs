using System;
using System.Threading.Tasks;

namespace NServiceBus.Backplane
{
    public interface IDataBackplaneClient
    {
        Task<IDataBackplaneSubscription> GetAllAndSubscribeToChanges(string type, Func<Entry, Task> onChanged, Func<Entry, Task> onRemoved);
        Task Publish(string type, string data);
        Task Revoke(string type);
    }

    public interface IDataBackplaneSubscription
    {
        void Unsubscribe();
    }
}