using System.IO;

namespace NServiceBus.Backplane.FileSystem.Internal
{
    internal class FileContent
    {
        private readonly string _ownerId;
        private readonly string _type;
        private readonly string _data;

        public FileContent(string ownerId, string type, string data)
        {
            _ownerId = ownerId;
            _type = type;
            _data = data;
        }

        public byte[] Encode()
        {
            using (var memStream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(memStream))
                {
                    writer.Write(_ownerId);
                    writer.Write(_type);
                    writer.Write(_data);
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