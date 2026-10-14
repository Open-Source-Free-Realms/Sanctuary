using System.Linq;

using Sanctuary.Core.IO;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

public static class ItemDefinitionHelper
{
    public const int PetItemType = 2;
    public const int PetClothingItemType = 3;
    public const int PetUtilityItemType = 4;
    public const int MountItemType = 19;

    private const int NoPurchasesLeftMaxStackSize = 0;

    public static ClientItemDefinition GetPlayerItemDefinition(Player player, IResourceManager resourceManager, ClientItemDefinition clientItemDefinition)
    {
        if (clientItemDefinition.Type != MountItemType
            || clientItemDefinition.MaxStackSize <= NoPurchasesLeftMaxStackSize
            || player.Mounts.Count(mount => mount.Definition == clientItemDefinition.Param1) < clientItemDefinition.MaxStackSize
            || HasTintChoice(resourceManager, clientItemDefinition))
            return clientItemDefinition;

        var playerItemDefinition = clientItemDefinition.Clone();

        playerItemDefinition.MaxStackSize = NoPurchasesLeftMaxStackSize;

        return playerItemDefinition;
    }

    public static void SendItemDefinition(Player player, IResourceManager resourceManager, ClientItemDefinition clientItemDefinition)
    {
        using var writer = new PacketWriter();

        GetPlayerItemDefinition(player, resourceManager, clientItemDefinition).Serialize(writer);

        var playerUpdatePacketItemDefinitionRequest = new PlayerUpdatePacketItemDefinitionRequest
        {
            Id = clientItemDefinition.Id,
            Payload = writer.Buffer
        };

        player.SendTunneled(playerUpdatePacketItemDefinitionRequest);
    }

    private static bool HasTintChoice(IResourceManager resourceManager, ClientItemDefinition clientItemDefinition) =>
        resourceManager.Stores.Values.Any(storeDefinition => storeDefinition.Bundles.Values.Any(appStoreBundleDefinition =>
            appStoreBundleDefinition.IsTintable && appStoreBundleDefinition.Entries.Any(entry => entry.MarketingItemId == clientItemDefinition.Id)));
}
