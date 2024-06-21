using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public struct PlayerUpdatePacketCameraUpdate : IDeserializable<PlayerUpdatePacketCameraUpdate>
{
    public const short OpCode = 126;

    public Vector4 Position;
    public Quaternion Rotation;

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PlayerUpdatePacketCameraUpdate value)
    {
        value = default;

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) && opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.Position, true))
            return false;

        if (!reader.TryRead(out value.Rotation, true))
            return false;

        return reader.RemainingLength == 0;
    }
}