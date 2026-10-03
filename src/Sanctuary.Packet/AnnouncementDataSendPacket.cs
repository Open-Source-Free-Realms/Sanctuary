using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public sealed class AnnouncementDataSendPacket : ISerializablePacket
{
    public const short OpCode = 193;
    public const byte SubOpCode = 2;

    public List<AnnouncementInfo> Announcements = [];

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        writer.Write(OpCode);
        writer.Write(SubOpCode);
        writer.Write(Announcements);
        return writer.Buffer;
    }
}
