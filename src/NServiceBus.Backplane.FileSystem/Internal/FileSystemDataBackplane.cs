using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NServiceBus.Backplane.FileSystem.Internal
{
    internal class FileSystemDataBackplane : IDataBackplane
    {
        private readonly string _ownerId;
        private readonly string _folder;

        public FileSystemDataBackplane(string ownerId, string folder)
        {
            _ownerId = ownerId;
            _folder = folder;
        }

        public Task Publish(string type, string data)
        {
            var content = new FileContent(_ownerId, type, data);
            var path = CreateFilePath(type);
            File.WriteAllBytes(path, content.Encode());
            return Task.FromResult(0);
        }

        private string CreateFilePath(string type)
        {
            var bytes = Encoding.UTF8.GetBytes(_ownerId + type);
            var hashstring = new SHA256Managed();
            var hash = hashstring.ComputeHash(bytes);
            var hashString = string.Empty;
            foreach (var x in hash)
            {
                hashString += $"{x:x2}";
            }
            return Path.Combine(_folder, hashString);
        }

        public Task<IReadOnlyCollection<Entry>> Query()
        {
            var allFiles = Directory.GetFiles(_folder);

            IReadOnlyCollection<Entry> result = allFiles
                .Where(f => File.GetLastWriteTimeUtc(f) > DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(10)))
                .Select(s => TryReadContent(s))
                .Where(c => c != null)
                .Select(FileContent.Decode)
                .ToArray();

            return Task.FromResult(result);
        }

        private static byte[] TryReadContent(string s)
        {
            try
            {
                return File.ReadAllBytes(s);
            }
            catch (IOException)
            {
                return null;
            }
        }

        public Task Revoke(string type)
        {
            var path = CreateFilePath(type);
            File.Delete(path);
            return Task.FromResult(0);
        }
    }
}