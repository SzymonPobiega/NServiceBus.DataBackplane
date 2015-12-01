using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NServiceBus.Backplane.FileSystem
{
    public class FileSystemDataBackplane : IDataBackplane
    {
        private readonly string ownerId;
        private readonly string folder;

        public FileSystemDataBackplane(string ownerId, string folder)
        {
            this.ownerId = ownerId;
            this.folder = folder;
        }

        public Task Publish(string type, string data)
        {
            var content = new FileContent(ownerId, type, data);
            var path = CreateFilePath(type);
            File.WriteAllBytes(path, content.Encode());
            return Task.FromResult(0);
        }

        private string CreateFilePath(string type)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(ownerId + type);
            var hashstring = new SHA256Managed();
            byte[] hash = hashstring.ComputeHash(bytes);
            string hashString = string.Empty;
            foreach (byte x in hash)
            {
                hashString += $"{x:x2}";
            }
            return Path.Combine(folder, hashString);
        }

        public Task<IReadOnlyCollection<Entry>> Query()
        {
            var allFiles = Directory.GetFiles(folder);

            IReadOnlyCollection<Entry> result = allFiles
                .Select(File.ReadAllBytes)
                .Select(FileContent.Decode)
                .ToArray();

            return Task.FromResult(result);
        }

        public Task Revoke(string type)
        {
            var path = CreateFilePath(type);
            File.Delete(path);
            return Task.FromResult(0);
        }
    }

    public class FileContent
    {
        private readonly string ownerId;
        private readonly string type;
        private readonly string data;

        public FileContent(string ownerId, string type, string data)
        {
            this.ownerId = ownerId;
            this.type = type;
            this.data = data;
        }

        public byte[] Encode()
        {
            using (var memStream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(memStream))
                {
                    writer.Write(ownerId);
                    writer.Write(type);
                    writer.Write(data);
                }
                return memStream.ToArray();
            }
        }

        public static Entry Decode(byte[] content)
        {
            var reader = new BinaryReader(new MemoryStream(content));
            var owner = reader.ReadString();
            var type = reader.ReadString();
            var data = reader.ReadString();

            return new Entry(owner, type, data);
        }
    }
}