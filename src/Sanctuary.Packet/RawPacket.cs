using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public sealed class RawPacket : ISerializablePacket
{
    private readonly byte[] _payload;

    public RawPacket(byte[] payload)
    {
        _payload = payload;
    }

    public byte[] Serialize() => _payload;
}
