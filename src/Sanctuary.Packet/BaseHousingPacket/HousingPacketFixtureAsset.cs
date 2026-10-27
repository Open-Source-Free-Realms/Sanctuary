using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class HousingPacketFixtureAsset : BaseHousingPacket, ISerializablePacket
{
    public new const short OpCode = 42;

    /// <summary>
    /// Model the client builds the placement preview from.
    /// </summary>
    public int ModelId;

    /// <summary>
    /// Echoed back by the client in <see cref="ClientHousingPacketPlaceFixture"/>.
    /// </summary>
    public int ItemGuid;

    public FixtureDefinition Definition = new();

    /// <summary>
    /// Texture variant of the preview, the same aliases items use.
    /// </summary>
    public string? TextureAlias;
    public string? TintAlias;

    public int Unknown3;
    public int TintId;

    public string? Unknown5;

    public List<ulong> Unknown6 = new();

    public int Unknown7;
    public int Unknown8;

    /// <summary>
    /// Places the fixture at the last placement location instead of
    /// letting the player position it. Used when placing duplicates.
    /// </summary>
    public bool PlaceImmediately;

    public HousingPacketFixtureAsset() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ModelId);
        writer.Write(ItemGuid);

        Definition.Serialize(writer);

        writer.Write(TextureAlias);
        writer.Write(TintAlias);

        writer.Write(Unknown3);
        writer.Write(TintId);

        writer.Write(Unknown5);

        writer.Write(Unknown6);

        writer.Write(Unknown7);
        writer.Write(Unknown8);

        writer.Write(PlaceImmediately);

        return writer.Buffer;
    }
}