using Sanctuary.Packet;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Entities;

public class Mount : Npc
{
    public Player Rider { get; init; }
    public MountDefinition Definition { get; init; }

    public Mount(Player rider, MountDefinition definition)
    {
        Rider = rider;
        Definition = definition;
    }

    public override void OnEntityAdd(IEntity entity)
    {
        // Mount

        base.OnEntityAdd(entity);
    }

    public override void OnEntityRemove(IEntity entity)
    {
        base.OnEntityRemove(entity);
    }

    public override PlayerUpdatePacketAddNpc GetAddNpcPacket()
    {
        var packet = base.GetAddNpcPacket();

        packet.RiderGuid = Rider.Guid;

        return packet;
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}