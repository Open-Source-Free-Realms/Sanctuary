using System.Collections.Generic;
using System.Linq;

using Sanctuary.Core.IO;
using Sanctuary.Game.Entities;

namespace Sanctuary.Game.Groups;

public sealed class PlayerGroup
{
    public const int MaxMembers = 5;

    public IReadOnlyList<Player> Members { get; private set; }

    public Player Leader => Members[0];

    public bool IsFull => Members.Count >= MaxMembers;

    public PlayerGroup(Player leader)
    {
        Members = [leader];
    }

    public void Add(Player player)
    {
        Members = [.. Members, player];
    }

    public void Remove(Player player)
    {
        Members = Members.Where(member => member.Guid != player.Guid).ToList();
    }

    public void SendTunneled(ISerializablePacket serializablePacket, Player? except = null)
    {
        var data = Player.SerializeTunneled(serializablePacket);

        foreach (var member in Members)
        {
            if (except is null || member.Guid != except.Guid)
                member.SendSerialized(data);
        }
    }
}
