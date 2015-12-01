using System;
using System.Threading.Tasks;

namespace NServiceBus.Backplane
{
    /// <summary>
    /// Provides access to the data backplane.
    /// </summary>
    public interface IDataBackplaneClient
    {
        /// <summary>
        /// Returns the current list off all entries matching the specified type (blocking) and subscribes to future changes. The method does not
        /// return untill all current entries has been processed by <paramref name="onChanged"/> callback.
        /// </summary>
        /// <param name="type">Type of entry to query.</param>
        /// <param name="onChanged">Callback invoked when an entry is added or updated or when processing current list.</param>
        /// <param name="onRemoved">Callback invoked when an entry is removed</param>
        /// <returns>Object that allows to cease the subscription.</returns>
        Task<IDataBackplaneSubscription> GetAllAndSubscribeToChanges(string type, Func<Entry, Task> onChanged, Func<Entry, Task> onRemoved);

        /// <summary>
        /// Publishes a new entry or updates an already published entry.
        /// </summary>
        /// <param name="type">Entry type</param>
        /// <param name="data">Data to publish.</param>
        Task Publish(string type, string data);

        /// <summary>
        /// Revokes an existing publication.
        /// </summary>
        /// <param name="type">Entry type.</param>
        Task Revoke(string type);
    }
}