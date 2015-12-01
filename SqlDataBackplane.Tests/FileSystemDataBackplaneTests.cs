using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NServiceBus.Backplane.FileSystem;
using NServiceBus.Backplane.SqlServer;
using NUnit.Framework;

namespace SqlDataBackplane.Tests
{
    [TestFixture]
    public class FileSystemDataBackplaneTests
    {
        [Test]
        public async Task Own_published_value_can_be_read_and_revoked()
        {
            var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataBackplane");
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
            Directory.CreateDirectory(folder);

            var backplane = new FileSystemDataBackplane("A", folder);

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
