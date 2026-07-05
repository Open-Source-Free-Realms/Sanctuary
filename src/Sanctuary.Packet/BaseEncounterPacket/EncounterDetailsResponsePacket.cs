using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class EncounterDetailsResponsePacket : BaseEncounterPacket, ISerializablePacket
{
    public new const short OpCode = 114;

    public int EncounterType { get; set; } = 7;
    public int EncounterId { get; set; }
    public int NameId { get; set; }
    public int DescriptionId { get; set; }
    public int Difficulty { get; set; } = 1;
    public int DetailImageId { get; set; }
    public int ThumbnailImageId { get; set; }
    public bool CanEnter { get; set; } = true;
    public int RewardPreviewBundleId { get; set; }

    public EncounterDetailsResponsePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);
        WriteEncounterDetailsCommon(writer);

        writer.Write(CanEnter);
        writer.Write(RewardPreviewBundleId);
        writer.Write(0); // Store-bundle id set count.

        return writer.Buffer;
    }

    internal void WriteEncounterDetailsCommon(PacketWriter writer)
    {
        writer.Write(EncounterType);
        writer.Write(EncounterId);

        writer.Write(0); // Participant teams.
        writer.Write(0); // Encounter teams.

        writer.Write(NameId);
        writer.Write(DescriptionId);

        writer.Write(false);
        writer.Write(false);
        writer.Write(false);

        writer.Write(Difficulty);
        writer.Write(DetailImageId);

        WriteRewardBundle(writer);

        writer.Write(false);
        writer.Write(true);
    }

    private void WriteRewardBundle(PacketWriter writer)
    {
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(ThumbnailImageId);
        writer.Write(false);

        WriteRewardBundleEntry(writer);
        WriteRewardBundleEntry(writer);
        WriteRewardBundleEntry(writer);

        writer.Write(0); // Fixed string list.

        writer.Write(false);
        writer.Write(false);
        writer.Write(false);
        writer.Write(false);

        writer.Write(0); // Opaque payload.
        writer.Write(0);
        writer.Write(false);
        writer.Write(0);
        writer.Write(false);
        writer.Write(false);
        writer.Write(false);
        writer.Write(false);
        writer.Write(false);
        writer.Write(0);
    }

    private void WriteRewardBundleEntry(PacketWriter writer)
    {
        writer.Write(false);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(1.0f);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0L);
        writer.Write(0L);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
    }
}
