using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NServiceBus.Backplane
{
    class DataBackplaneClient : IDataBackplaneClient
    {
        private Dictionary<CacheKey, Entry> cache = new Dictionary<CacheKey, Entry>();
        private readonly IDataBackplane dataBackplane;
        private readonly IQuerySchedule schedule;
        private IDisposable timer;
        private readonly ConcurrentDictionary<Guid, Subscriber> subscribers = new ConcurrentDictionary<Guid, Subscriber>();

        public DataBackplaneClient(IDataBackplane dataBackplane, IQuerySchedule schedule)
        {
            this.dataBackplane = dataBackplane;
            this.schedule = schedule;
        }

        public Task Start()
        {
            timer = schedule.Schedule(async () =>
            {
                var addedOrUpdated = new List<Entry>();
                var results = (await dataBackplane.Query());
                var removed = cache.Values.Where(x => !results.Any(r => r.Type == x.Type && r.Owner == x.Owner)).ToArray();
                foreach (var entry in results)
                {
                    Entry oldEntry;
                    var key = new CacheKey(entry.Owner, entry.Type);
                    if (!cache.TryGetValue(key, out oldEntry) || oldEntry.Data != entry.Data)
                    {
                        cache[key] = entry;
                        addedOrUpdated.Add(entry);
                    }
                }
                foreach (var change in addedOrUpdated)
                {
                    await NotifyChanged(change).ConfigureAwait(false);
                }
                foreach (var entry in removed)
                {
                    cache.Remove(new CacheKey(entry.Owner, entry.Type));
                    await NotifyRemoved(entry).ConfigureAwait(false);
                }
            });
            return Task.FromResult(0);
        }

        private async Task NotifyChanged(Entry change)
        {
            foreach (var subscriber in subscribers)
            {
                await subscriber.Value.NotifyChanged(change).ConfigureAwait(false);
            }
        }

        private async Task NotifyRemoved(Entry change)
        {
            foreach (var subscriber in subscribers)
            {
                await subscriber.Value.NotifyRemoved(change).ConfigureAwait(false);
            }
        }

        public Task Stop()
        {
            timer.Dispose();
            return Task.FromResult(0);
        }

        public Task Publish(string type, string data)
        {
            return dataBackplane.Publish(type, data);
        }

        public Task Revoke(string type)
        {
            return dataBackplane.Revoke(type);
        }

        public async Task<IDataBackplaneSubscription> GetAllAndSubscribeToChanges(string type, Func<Entry, Task> onChanged, Func<Entry, Task> onRemoved)
        {
            var subscriberId = Guid.NewGuid();
            var subscriber = new Subscriber(type, onChanged, onRemoved, () =>
            {
                Subscriber _;
                subscribers.TryRemove(subscriberId, out _);
            });
            subscribers.TryAdd(subscriberId, subscriber);
            var cacheCopy = cache;
            foreach (var entry in cacheCopy)
            {
                await subscriber.NotifyChanged(entry.Value).ConfigureAwait(false);
            }
            return subscriber;
        }

        private class Subscriber : IDataBackplaneSubscription
        {
            private readonly Func<Entry, Task> onAddedOrUpdated;
            private readonly Func<Entry, Task> onRemoved;
            private readonly Action unsubscribe;
            private readonly string type;
            private bool unsubscribed;

            public Subscriber(string type, Func<Entry, Task> onAddedOrUpdated, Func<Entry, Task> onRemoved, Action unsubscribe)
            {
                this.onAddedOrUpdated = onAddedOrUpdated;
                this.onRemoved = onRemoved;
                this.unsubscribe = unsubscribe;
                this.type = type;
            }

            public Task NotifyChanged(Entry changedEntry)
            {
                if (type == changedEntry.Type)
                {
                    return onAddedOrUpdated(changedEntry);
                }
                return Task.FromResult(0);
            }

            public Task NotifyRemoved(Entry removedEntry)
            {
                if (type == removedEntry.Type)
                {
                    return onRemoved(removedEntry);
                }
                return Task.FromResult(0);
            }

            public void Unsubscribe()
            {
                if (unsubscribed)
                {
                    return;
                }
                unsubscribe();
                unsubscribed = true;
            }
        }
        
        private class CacheKey
        {
            private readonly string owner;
            private readonly string type;

            public CacheKey(string owner, string type)
            {
                this.owner = owner;
                this.type = type;
            }

            protected bool Equals(CacheKey other)
            {
                return string.Equals(owner, other.owner) && string.Equals(type, other.type);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((CacheKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (owner.GetHashCode() * 397) ^ type.GetHashCode();
                }
            }

            public static bool operator ==(CacheKey left, CacheKey right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(CacheKey left, CacheKey right)
            {
                return !Equals(left, right);
            }
        }
    }
}