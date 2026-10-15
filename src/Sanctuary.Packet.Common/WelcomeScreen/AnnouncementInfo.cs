using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class AnnouncementInfo : ISerializableType
{
    public int Id;
    public int Priority;
    public int IconId;
    public int TitleStringId;
    public int BodyStringId;
    public int ButtonStringId;
    public string LuaCall = string.Empty;
    public string StringParam1 = string.Empty;
    public int Param1;
    public int Param2;
    public int Param3;
    public string StringParam2 = string.Empty;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(Priority);
        writer.Write(IconId);
        writer.Write(TitleStringId);
        writer.Write(BodyStringId);
        writer.Write(ButtonStringId);
        writer.Write(LuaCall);
        writer.Write(StringParam1);
        writer.Write(Param1);
        writer.Write(Param2);
        writer.Write(Param3);
        writer.Write(StringParam2);
    }
}
