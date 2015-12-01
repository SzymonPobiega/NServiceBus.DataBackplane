using System.Collections.Generic;
using System.Threading.Tasks;

namespace NServiceBus.Backplane
{
    public interface IDataBackplane
    {
        Task Publish(string type, string data);
        Task<IReadOnlyCollection<Entry>> Query();
        Task Revoke(string type);
    }
}