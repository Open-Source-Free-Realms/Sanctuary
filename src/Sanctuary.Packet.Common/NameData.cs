using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class NameData
{
    public string FirstName = string.Empty;
    public string LastName = string.Empty;

    public string FullName => string.IsNullOrWhiteSpace(LastName)
        ? FirstName
        : $"{FirstName} {LastName}";

    public int FirstNameId;
    public int LastNamePrefixId;
    public int LastNameSuffixId;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(FirstNameId);
        writer.Write(LastNamePrefixId);
        writer.Write(LastNameSuffixId);

        writer.Write(FirstName);
        writer.Write(LastName);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out FirstNameId))
            return false;

        if (!reader.TryRead(out LastNamePrefixId))
            return false;

        if (!reader.TryRead(out LastNameSuffixId))
            return false;

        if (!reader.TryRead(out FirstName))
            return false;

        if (!reader.TryRead(out LastName))
            return false;

        return true;
    }
}