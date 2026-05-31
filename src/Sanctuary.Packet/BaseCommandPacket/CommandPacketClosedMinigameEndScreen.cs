using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class CommandPacketClosedMinigameEndScreen : BaseCommandPacket, IDeserializable<CommandPacketClosedMinigameEndScreen>
{
    public new const short OpCode = 42;

    public CommandPacketClosedMinigameEndScreen() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out CommandPacketClosedMinigameEndScreen value)
    {
        value = new CommandPacketClosedMinigameEndScreen();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}