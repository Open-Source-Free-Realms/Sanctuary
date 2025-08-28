using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;

namespace Sanctuary.Game.Entities;

public class Mount : Npc
{
    public Player Rider { get; init; }
    public MountDefinition Definition { get; init; }

    public int Seat { get; set; }
    public int QueuePosition { get; set; }

    public Mount(IZone zone, Player rider, MountDefinition definition) : base(zone)
    {
        Rider = rider;
        Definition = definition;
    }

    public override void TeleportToZone(IZone zone, Vector4 position, Quaternion rotation)
    {
        // Alert/Remove visible entities
        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.OnRemoveVisibleNpcs([this]);

        OnRemoveVisiblePlayers(VisiblePlayers.Values);

        ZoneTile.Entities.Remove(Guid, out _);

        Zone.TryRemoveNpc(Guid);

        // Add to new zone/zonetile

        zone.TryAddMount(this);

        // Teleport to new zone

        Visible = false;

        Zone = zone;

        ZoneTile = ZoneTile.Empty;

        UpdatePosition(position, rotation);
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