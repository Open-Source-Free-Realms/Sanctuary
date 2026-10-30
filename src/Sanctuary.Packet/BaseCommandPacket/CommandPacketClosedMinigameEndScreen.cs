using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class CommandPacketClosedMinigameEndScreen : BaseCommandPacket, ISerializablePacket, IDeserializable<CommandPacketClosedMinigameEndScreen>
{
    public new const short OpCode = 42;


    public CommandPacketClosedMinigameEndScreen() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);


        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out CommandPacketClosedMinigameEndScreen commandPacketClosedMinigameEndScreen)
    {
        commandPacketClosedMinigameEndScreen = new CommandPacketClosedMinigameEndScreen();

        var reader = new PacketReader(data);

        if (!commandPacketClosedMinigameEndScreen.TryRead(ref reader))
            return false;


        return reader.RemainingLength == 0;
    }
}
