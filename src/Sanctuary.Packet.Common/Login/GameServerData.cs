using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class GameServerData
{
    public int Id;

    [Flags]
    public enum States : byte
    {
        Locked = 1 << 0,
        Offline = 1 << 1
    }

    public States State;

    // The below values aren't read by the game

    public string? Name;
    public int NameId;

    public string? Description;
    public int DescriptionId;

    public int RequiredFeatureId;

    public string? ApplicationData;

    public int PopulationLevel;
    public string? PopulationOobData;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);

        writer.Write(State);

        writer.Write(Name);
        writer.Write(NameId);

        writer.Write(Description);
        writer.Write(DescriptionId);

        writer.Write(RequiredFeatureId);

        writer.Write(ApplicationData);

        writer.Write(PopulationLevel);
        writer.Write(PopulationOobData);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out Id))
            return false;

        if (!reader.TryRead(out State))
            return false;

        if (!reader.TryRead(out Name))
            return false;

        if (!reader.TryRead(out NameId))
            return false;

        if (!reader.TryRead(out Description))
            return false;

        if (!reader.TryRead(out DescriptionId))
            return false;

        if (!reader.TryRead(out RequiredFeatureId))
            return false;

        if (!reader.TryRead(out ApplicationData))
            return false;

        if (!reader.TryRead(out PopulationLevel))
            return false;

        if (!reader.TryRead(out PopulationOobData))
            return false;

        return true;
    }
}