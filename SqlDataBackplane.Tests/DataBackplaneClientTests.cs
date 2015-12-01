using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Backplane;
using NUnit.Framework;

namespace SqlDataBackplane.Tests
{
    [TestFixture]
    public class DataBackplaneClientTests
    {
        [Test]
        public async Task It_notifies_about_all_relevant_entries_right_after_subscribing()
        {
            var fakeSchedule = new FakeSchedule();
            var entries = new List<Entry>();
            entries.Add(new Entry("B", "T1", "B"));
            entries.Add(new Entry("C", "T1", "C"));
            var fakeBackplane = new FakeBackplane("A", entries);

            var client = new DataBackplaneClient(fakeBackplane, fakeSchedule);
            await client.Start();
            await fakeSchedule.TriggerQuery();
            var readValues = new List<string>();
            var subscription = await client.GetAllAndSubscribeToChanges("T1", entry =>
            {
                readValues.Add(entry.Data);
                return Task.FromResult(0);
            }, entry => Task.FromResult(0));
            CollectionAssert.AreEqual(new [] {"B", "C"}, readValues);
            subscription.Dispose();
        }

        [Test]
        public async Task It_keeps_notifying_about_entry_changes_when_subscription_is_active()
        {
            var fakeSchedule = new FakeSchedule();
            var entries = new List<Entry>();
            var fakeBackplane = new FakeBackplane("A", entries);
            var otherBackplane = new FakeBackplane("B", entries);

            var client = new DataBackplaneClient(fakeBackplane, fakeSchedule);
            await client.Start();
            var readValues = new List<string>();
            var subscription = await client.GetAllAndSubscribeToChanges("T1", entry =>
            {
                readValues.Add(entry.Data);
                return Task.FromResult(0);
            }, entry => Task.FromResult(0));

            await otherBackplane.Publish("T1", "B");
            await fakeSchedule.TriggerQuery();
            CollectionAssert.Contains(readValues, "B");
            subscription.Dispose();

            await otherBackplane.Publish("T1", "C");
            await fakeSchedule.TriggerQuery();
            CollectionAssert.DoesNotContain(readValues, "C");
        }

        [Test]
        public async Task It_notifies_about_removing_an_entry()
        {
            var fakeSchedule = new FakeSchedule();
            var entries = new List<Entry>();
            var fakeBackplane = new FakeBackplane("A", entries);
            var otherBackplane = new FakeBackplane("B", entries);

            var client = new DataBackplaneClient(fakeBackplane, fakeSchedule);
            await client.Start();
            var readValues = new List<string>();
            var subscription = await client.GetAllAndSubscribeToChanges("T1", entry =>
            {
                readValues.Add(entry.Data);
                return Task.FromResult(0);
            }, entry =>
            {
                readValues.Remove(entry.Data);
                return Task.FromResult(0);
            });

            await otherBackplane.Publish("T1", "B");
            await fakeSchedule.TriggerQuery();
            CollectionAssert.Contains(readValues, "B");

            await otherBackplane.Revoke("T1");
            await fakeSchedule.TriggerQuery();
            CollectionAssert.DoesNotContain(readValues, "B");
            subscription.Dispose();
        }

        private class FakeSchedule : IQuerySchedule
        {
            private Func<Task> action; 

            public IDisposable Schedule(Func<Task> recurringAction)
            {
                action = recurringAction;
                return new Disposable();
            }

            public Task TriggerQuery()
            {
                return action();
            }

            private class Disposable : IDisposable
            {
                public void Dispose()
                {
                }
            }
        }

        private class FakeBackplane : IDataBackplane
        {
            private readonly List<Entry> entries;
            private readonly string owner;

            public FakeBackplane(string owner, List<Entry> entries)
            {
                this.owner = owner;
                this.entries = entries;
            }


            public Task Publish(string type, string data)
            {
                entries.Add(new Entry(owner, type, data));
                return Task.FromResult(0);
            }

            public Task<IReadOnlyCollection<Entry>> Query()
            {
                return Task.FromResult((IReadOnlyCollection<Entry>)entries.ToArray());
            }

            public Task Revoke(string type)
            {
                entries.RemoveAll(e => e.Owner == owner && e.Type == type);
                return Task.FromResult(0);
            }
        }
    }
}