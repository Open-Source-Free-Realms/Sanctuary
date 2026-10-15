using System.Threading;

using Sanctuary.Game.Zones;

namespace Sanctuary.Game.Entities;

public sealed class PetTreasureChest : Npc
{
    private int _opened;

    public ulong OwnerGuid { get; }
    public string RewardTableKey { get; }
    public string? CoinRewardTableKey { get; set; }
    public int OpenCompositeEffectId { get; set; }

    public PetTreasureChest(IZone zone, ulong ownerGuid, string rewardTableKey)
        : base(zone)
    {
        OwnerGuid = ownerGuid;
        RewardTableKey = rewardTableKey;
    }

    public bool IsOwner(Player player) => OwnerGuid == player.Guid;

    public bool TryOpen()
    {
        return Interlocked.CompareExchange(ref _opened, 1, 0) == 0;
    }
}
