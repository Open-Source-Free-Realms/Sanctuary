using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

[System.Flags]
public enum CharacterStatus
{
    None = 0,
    IsNonAttackable = 0x1,
    IsAfraid = 0x2,
    IsAsleep = 0x4,
    IsSilenced = 0x8,
    IsBound = 0x10,
    IsRooted = 0x20,
    IsStunned = 0x40,
    IsKnockedOut = 0x80,
    IsNonAttackable2 = 0x100,
    IsKnockedBack = 0x200,
    IsConfused = 0x2000,
    IsGoingHome = 0x4000,
    IsBoss = 0x8000,
    IsFrozen = 0x10000,
    IsBerserk = 0x20000,
    IsScriptedAnimation = 0x40000,
    IsPoppedUp = 0x100000,
}


public class PlayerUpdatePacketUpdateCharacterState : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 20;

    public ulong Guid;
    public CharacterStatus Status;

    public PlayerUpdatePacketUpdateCharacterState() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write((int)Status);

        return writer.Buffer;
    }
}
