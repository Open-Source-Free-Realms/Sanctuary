using System;
using System.Numerics;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Packet;

public class PacketChat : BaseChatPacket, ISerializablePacket, IDeserializable<PacketChat>
{
    public new const short OpCode = 1;

    public ChatChannel Channel;

    public ulong FromGuid;
    public ulong ToGuid;

    public NameData FromName = new();
    public NameData ToName = new();

    public string? Message = null!;

    private Vector4 Position;

    public ulong GuildGuid;

    public int LanguageId;

    /// <summary>
    /// Only needed for <see cref="ChatChannel.Area"/>.
    /// </summary>
    public int AreaNameId;

    public PacketChat() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        writer.Write(Channel);

        writer.Write(FromGuid);
        writer.Write(ToGuid);

        FromName.Serialize(writer);
        ToName.Serialize(writer);

        writer.Write(Message);

        writer.Write(Position);

        writer.Write(GuildGuid);

        writer.Write(LanguageId);

        if (Channel == ChatChannel.Area)
            writer.Write(AreaNameId);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PacketChat value)
    {
        value = new PacketChat();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Channel))
            return false;

        if (!reader.TryRead(out value.FromGuid))
            return false;

        if (!reader.TryRead(out value.ToGuid))
            return false;

        if (!value.FromName.TryRead(ref reader))
            return false;

        if (!value.ToName.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Message))
            return false;

        if (!reader.TryRead(out value.Position))
            return false;

        if (!reader.TryRead(out value.GuildGuid))
            return false;

        if (!reader.TryRead(out value.LanguageId))
            return false;

        if (value.Channel == ChatChannel.Area)
        {
            if (!reader.TryRead(out value.AreaNameId))
                return false;
        }

        return reader.RemainingLength == 0;
    }
}