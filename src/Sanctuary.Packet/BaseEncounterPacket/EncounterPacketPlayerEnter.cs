using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op41 sub2 — marks the player entered into a launched encounter. This is what makes the client render
// the minigame HUD (goals panel, timer): creating the MiniGameState via the launch details packet is
// necessary but NOT sufficient — without this packet the HUD never shows.
public class EncounterPacketPlayerEnter : ISerializablePacket
{
    public const short OpCode = 41;
    public const short SubOpCode = 2;

    public int EncounterId;
    public int InstanceId;
    public ulong PlayerGuid;
    public byte Unknown;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);
        writer.Write(SubOpCode);

        writer.Write(EncounterId);
        writer.Write(InstanceId);
        writer.Write(PlayerGuid);
        writer.Write(Unknown);

        return writer.Buffer;
    }
}
