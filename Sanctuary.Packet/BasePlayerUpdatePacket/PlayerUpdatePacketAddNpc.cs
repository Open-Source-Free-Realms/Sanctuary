using System.Numerics;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketAddNpc : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 2;

    public ulong Guid;

    public int NameId;

    public int ModelId;

    public bool Unknown;

    public string TextureAlias = null!;
    public string TintAlias = null!;

    public int TintId;

    public int ChatBubbleForegroundColor = 0x063C67;
    public int ChatBubbleBackgroundColor = 0xD4E2F0;
    public int ChatBubbleSize = 1;

    public float Scale;

    [JsonConverter(typeof(Vector4JsonConverter))]
    public Vector4 Position;

    [JsonConverter(typeof(QuaternionJsonConverter))]
    public Quaternion Rotation;

    public List<CharacterAttachmentData> Attachments = new();

    public bool Unknown26;

    /// <summary>
    /// 0 - Hostile
    /// 1 - Neutral
    /// 2 - Ally
    /// </summary>
    public int Disposition;

    public int AnimSlotId;

    public bool Unknown16;

    public float VerticalOffset;

    public int CompositeEffectId;

    public int WieldType;

    public string Name = null!;

    public bool HideNamePlate;

    public float Unknown22;
    public float Unknown23;
    public float Unknown24;

    public int TerrainObjectId;

    public float Speed;

    public bool Unknown28;

    public int InteractRange;

    public int Unknown30;
    public int Unknown31;
    public int Unknown32;

    public bool Unknown33;
    public bool Unknown34;

    public int SubTextNameId;

    public int Unknown36;

    public int TemporaryAppearance;

    public List<EffectTag> EffectTags = new();

    public bool Unknown38;

    public int Unknown39;

    public bool Unknown40;
    public bool Unknown41;
    public bool Unknown42;

    public bool HasTilt;

    public CustomizationDetail Customization = new();

    [JsonConverter(typeof(Vector4JsonConverter))]
    public Vector4 Tilt;

    public int Unknown45;

    public int AreaDefinitionId;

    public int ImageSetId;

    public bool IsInteractable;

    public ulong RiderGuid;

    public int Unknown50;

    public float Unknown51;

    // public Target Target;
    // public CharacterVariables Variables;

    public int Unknown52;

    public float Unknown53;

    [JsonConverter(typeof(Vector4JsonConverter))]
    public Vector4 Unknown54;

    public int Unknown55;

    public float Unknown56;
    public float Unknown57;
    public float Unknown58;

    public string Head = null!;
    public string Hair = null!;
    public string ModelCustomization = null!;

    /// <summary>
    /// Replaces the terrain object based on the model id.
    /// </summary>
    public bool ReplaceTerrainObject;

    public int Unknown63;
    public int Unknown64;

    /// <summary>
    /// Fly-by composite effect id
    /// </summary>
    public int FlyByEffectId;

    public int ActiveProfile;

    public int Unknown67;
    public int Unknown68;

    public float NameScale;

    public int NameplateImageId;

    public PlayerUpdatePacketAddNpc() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        writer.Write(NameId);

        writer.Write(ModelId);

        writer.Write(Unknown);

        writer.Write(ChatBubbleForegroundColor);
        writer.Write(ChatBubbleBackgroundColor);
        writer.Write(ChatBubbleSize);

        writer.Write(Scale);

        writer.Write(Position);
        writer.Write(Rotation);

        writer.Write(AnimSlotId);

        writer.Write(Attachments);

        writer.Write(Disposition);

        writer.Write(TextureAlias);
        writer.Write(TintAlias);

        writer.Write(TintId);

        writer.Write(Unknown16);

        writer.Write(VerticalOffset);

        writer.Write(CompositeEffectId);

        writer.Write(WieldType);

        writer.Write(Name);

        writer.Write(HideNamePlate);

        writer.Write(Unknown22);

        writer.Write(Unknown23);
        writer.Write(Unknown24);

        writer.Write(TerrainObjectId);

        writer.Write(Unknown26);

        writer.Write(Speed);

        writer.Write(Unknown28);

        writer.Write(InteractRange);

        writer.Write(Unknown30);
        writer.Write(Unknown31);

        writer.Write(Unknown32);

        writer.Write(Unknown33);
        writer.Write(Unknown34);

        writer.Write(SubTextNameId);

        writer.Write(Unknown36);

        writer.Write(TemporaryAppearance);

        writer.Write(EffectTags);

        writer.Write(Unknown38);

        writer.Write(Unknown39);

        writer.Write(Unknown40);
        writer.Write(Unknown41);
        writer.Write(Unknown42);
        writer.Write(HasTilt);

        Customization.Serialize(writer);

        writer.Write(Tilt);

        writer.Write(Unknown45);
        writer.Write(AreaDefinitionId);
        writer.Write(ImageSetId);

        writer.Write(IsInteractable);

        writer.Write(RiderGuid);

        writer.Write(Unknown50);

        writer.Write(Unknown51);

        writer.Write(0); // Target

        writer.Write(0); // CharacterVariables

        writer.Write(Unknown52);

        writer.Write(Unknown53);

        writer.Write(Unknown54);

        writer.Write(Unknown55);

        writer.Write(Unknown56);
        writer.Write(Unknown57);
        writer.Write(Unknown58);

        writer.Write(Head);
        writer.Write(Hair);
        writer.Write(ModelCustomization);

        writer.Write(ReplaceTerrainObject);

        writer.Write(Unknown63);
        writer.Write(Unknown64);

        writer.Write(FlyByEffectId);

        writer.Write(ActiveProfile);

        writer.Write(Unknown67);
        writer.Write(Unknown68);

        writer.Write(NameScale);

        writer.Write(NameplateImageId);

        return writer.Buffer;
    }
}