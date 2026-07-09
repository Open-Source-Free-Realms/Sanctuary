using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class NotificationInfo : ISerializableType
{
    public ulong Guid { get; set; }

    /// <summary>
    /// Observed in packet logs. When true, the client expects the compact payload.
    /// </summary>
    public bool IsCompact { get; set; }

    public int NotificationType { get; set; }

    public int IconId { get; set; }
    public int IconState { get; set; }
    public int Unknown { get; set; }
    public int NameId { get; set; }
    public int ReferenceId { get; set; }
    public int Unknown2 { get; set; }

    public bool Unknown3 { get; set; }
    public bool Enabled { get; set; } = true;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Guid);
        writer.Write(IsCompact);

        if (IsCompact)
        {
            writer.Write(NotificationType);
            writer.Write(Enabled);
            return;
        }

        writer.Write(NotificationType);
        writer.Write(IconId);
        writer.Write(IconState);
        writer.Write(Unknown);
        writer.Write(NameId);
        writer.Write(ReferenceId);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Enabled);
    }
}
