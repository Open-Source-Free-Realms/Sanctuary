using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class MiniGameGroupInfo
{
    public int Id;

    public int NameId;
    public int DescriptionId;

    public int IconId;

    public int PreselectedGameId;

    public string? BackgroundSwf;
    public string? StageProgression;

    public bool ShowStartScreenOnPlayNext;

    public int SettingsIconId;

    // public List<MiniGameGroupElementInfo> Elements;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(Id);

        writer.Write(NameId);
        writer.Write(DescriptionId);

        writer.Write(IconId);

        writer.Write(BackgroundSwf);

        writer.Write(PreselectedGameId);

        // TODO: Elements
        writer.Write(0);

        writer.Write(StageProgression);

        writer.Write(ShowStartScreenOnPlayNext);

        writer.Write(SettingsIconId);

        return writer.Buffer;
    }
}