using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NServiceBus.Backplane.SqlServer;
using NUnit.Framework;

namespace SqlDataBackplane.Tests
{
    [TestFixture]
    public class DataPackplaneTests
    {
        [Test]
        public async Task Own_published_value_can_be_read_and_revoked()
        {
            var backplane =
                new SqlServerDataBackplane("A", @"Data Source=(local);Initial Catalog=Backplane1;Integrated Security=True");

            var value = Guid.NewGuid().ToString();
            var type = Guid.NewGuid().ToString();

            await backplane.Publish(type, value);

            var read = await backplane.Query();
            Assert.IsTrue(read.Any(x => x.Data == value && x.Type == type));

            await backplane.Revoke(type);

            read = await backplane.Query();
            Assert.IsFalse(read.Any(x => x.Data == value && x.Type == type));
        }
    }
}
