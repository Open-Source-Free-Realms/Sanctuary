using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class AccountInfoCharacterData
{
    public ulong Guid;
    public string? Name;
    public int Model;
    public string? Head;
    public string? Hair;
    public string? ModelCustomization;
    public string? FacePaint;
    public string? SkinTone;
    public int EyeTint;
    public int HairTint;
    public List<CharacterAttachmentData> CharacterAttachments = new();

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Guid);
        writer.Write(Name);
        writer.Write(Model);
        writer.Write(Head);
        writer.Write(Hair);
        writer.Write(ModelCustomization);
        writer.Write(FacePaint);
        writer.Write(SkinTone);
        writer.Write(EyeTint);
        writer.Write(HairTint);

        writer.Write(CharacterAttachments);
    }
}