using System;
using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class ActivityPacketListOfActivities : BaseActivityPacket, ISerializablePacket, IDeserializable<ActivityPacketListOfActivities>
{
    public new const byte OpCode = 1;

    public int ServerType;

    public List<ClientActivityDefinition> Activities = [];

    public ActivityPacketListOfActivities() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ServerType);
        writer.Write(Activities);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ActivityPacketListOfActivities activityPacketListOfActivities)
    {
        activityPacketListOfActivities = new ActivityPacketListOfActivities();

        var reader = new PacketReader(data);

        if (!activityPacketListOfActivities.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out activityPacketListOfActivities.ServerType))
            return false;

        if (!reader.TryRead(out int count) || count < 0)
            return false;

        for (int i = 0; i < count; i++)
        {
            var clientActivityDefinition = new ClientActivityDefinition();

            if (!clientActivityDefinition.TryRead(ref reader))
                return false;

            activityPacketListOfActivities.Activities.Add(clientActivityDefinition);
        }

        return reader.RemainingLength == 0;
    }
}
