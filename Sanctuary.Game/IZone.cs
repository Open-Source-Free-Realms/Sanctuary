using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Sanctuary.UdpLibrary;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game;

public interface IZone
{
    ZoneDefinition Definition { get; }

    IEnumerable<IEntity> Entities { get; }

    void RemoveEntity(IEntity entity);

    bool TryCreateNpc([MaybeNullWhen(false)] out Npc npc);
    bool TryCreateMount(Player rider, MountDefinition definition, [MaybeNullWhen(false)] out Mount mount);
    bool TryCreatePlayer(ulong guid, UdpConnection connection, [MaybeNullWhen(false)] out Player player);
}