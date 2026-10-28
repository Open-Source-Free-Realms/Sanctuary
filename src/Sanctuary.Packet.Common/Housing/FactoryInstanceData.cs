using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class FactoryInstanceData : ISerializableType, IDeserializableType
{
    private const int EmptyListCount = 0;

    public Dictionary<int, FactoryExperienceNode> ExperienceNodes = new();

    public ulong Guid;

    public bool Unknown5;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(ExperienceNodes);

        writer.Write(Guid);

        writer.Write(EmptyListCount);
        writer.Write(EmptyListCount);

        writer.Write(Unknown5);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out int experienceNodesCount) || experienceNodesCount < 0)
            return false;

        for (int i = 0; i < experienceNodesCount; i++)
        {
            if (!reader.TryRead(out int experienceNodeId))
                return false;

            var factoryExperienceNode = new FactoryExperienceNode();

            if (!factoryExperienceNode.TryRead(ref reader))
                return false;

            if (!ExperienceNodes.TryAdd(experienceNodeId, factoryExperienceNode))
                return false;
        }

        if (!reader.TryRead(out Guid))
            return false;

        if (!reader.TryRead(out int plotsCount) || plotsCount != EmptyListCount)
            return false;

        if (!reader.TryRead(out int misfortunesCount) || misfortunesCount != EmptyListCount)
            return false;

        if (!reader.TryRead(out Unknown5))
            return false;

        return true;
    }
}
