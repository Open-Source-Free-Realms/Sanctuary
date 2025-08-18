using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketAddPc : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 1;

    public ulong Guid;

    public NameData Name = new();

    public int Model;

    public int ChatBubbleForegroundColor;
    public int ChatBubbleBackgroundColor;
    public int ChatBubbleSize;

    public Vector4 Position;
    public Quaternion Rotation;

    public List<CharacterAttachmentData> Attachments = new();

    public string Head = null!;
    public string Hair = null!;

    public int HairColor;
    public int EyeColor;

    // Not read by client
    private int Unknown3 = default;

    public string SkinTone = null!;
    public string? FacePaint;
    public string? ModelCustomization;

    public float MaxMovementSpeed;

    public bool IsUnderage;
    public bool IsMember;
    public bool IsReferee;

    // Custom Model Id
    public int TemporaryAppearance;

    public List<ulong> Guilds = new();

    public int ActiveProfile;

    public PlayerTitleData Title = new();

    public ulong MountGuid;

    public int Unknown10;
    public int Unknown11;
    public float NameVerticalOffset;

    public int WieldType;

    public float VipRank;
    public int VipIconId;
    public int VipTitle;

    public int Unknown17;

    public PlayerUpdatePacketAddPc() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        Name.Serialize(writer);

        writer.Write(Model);

        writer.Write(ChatBubbleForegroundColor);
        writer.Write(ChatBubbleBackgroundColor);
        writer.Write(ChatBubbleSize);

        writer.Write(Position);
        writer.Write(Rotation);

        writer.Write(Attachments);

        writer.Write(Head);
        writer.Write(Hair);

        writer.Write(HairColor);
        writer.Write(EyeColor);

        writer.Write(Unknown3);

        writer.Write(SkinTone);
        writer.Write(FacePaint);
        writer.Write(ModelCustomization);

        writer.Write(MaxMovementSpeed);

        writer.Write(IsUnderage);
        writer.Write(IsMember);
        writer.Write(IsReferee);

        writer.Write(TemporaryAppearance);

        writer.Write(Guilds);

        writer.Write(ActiveProfile);

        Title.Serialize(writer);

        writer.Write(0); // EffectTags

        writer.Write(MountGuid);

        writer.Write(Unknown10);
        writer.Write(Unknown11);

        writer.Write(NameVerticalOffset);

        writer.Write(WieldType);

        writer.Write(VipRank);

        writer.Write(VipIconId);
        writer.Write(VipTitle);
        writer.Write(Unknown17);

        return writer.Buffer;
    }
}