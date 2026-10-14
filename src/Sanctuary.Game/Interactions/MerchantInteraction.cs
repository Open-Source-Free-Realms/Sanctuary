using System.Collections.Generic;
using System.Linq;

using Sanctuary.Core.IO;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Interactions;

public class MerchantInteraction : IInteraction
{
    public const int Type = 17;
    public static readonly InteractionData Data = new() { Id = IInteraction.UniqueId++, Type = Type };
    public int Id => Data.Id;
    private readonly IResourceManager _resourceManager;

    public MerchantInteraction(IResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public void OnInteract(Player player, IEntity other)
    {
        if (other is not Npc npc || npc.MerchantList is null ||
            npc.InteractionList?.Interactions.Any(x => x.Type == Type) != true ||
            !InteractionMenuHelper.CanInteract(npc, player))
            return;

        var coinStoreMerchantListPacket = new CoinStoreMerchantListPacket();
        coinStoreMerchantListPacket.MerchantList.Unknown = npc.MerchantList.Unknown;
        coinStoreMerchantListPacket.MerchantList.Unknown2 = unchecked((long)player.Guid);
        coinStoreMerchantListPacket.MerchantList.NpcGuid = npc.Guid;
        coinStoreMerchantListPacket.MerchantList.Unknown4 = npc.MerchantList.Unknown4;
        var definitions = new List<ClientItemDefinition>();

        foreach (var entry in npc.MerchantList.Entries)
        {
            if (!_resourceManager.ClientItemDefinitions.TryGetValue(entry.ItemDefinitionId, out var definition))
                continue;

            definitions.Add(definition);

            coinStoreMerchantListPacket.MerchantList.Entries.Add(new MerchantList.Entry
            {
                ItemDefinitionId = definition.Id,
                IconId = definition.Icon.Id,
                TintId = definition.Icon.TintId,
                NameId = definition.NameId,
                DescriptionId = definition.DescriptionId,
                PurchasableQty = entry.PurchasableQty,
                MembersOnly = entry.MembersOnly,
                Unknown8 = player.MembershipStatus == 0 ? definition.Cost : definition.GetMemberPurchasePrice(),
                Unknown9 = entry.Unknown9,
                AvailableTintGroupId = entry.AvailableTintGroupId,
                CanBuy = entry.CanBuy
            });
        }
        using var writer = new PacketWriter();

        writer.Write(definitions);

        var playerUpdatePacketItemDefinitions = new PlayerUpdatePacketItemDefinitions
        {
            Payload = writer.Buffer
        };

        player.SendTunneled(playerUpdatePacketItemDefinitions);
        player.SendTunneled(coinStoreMerchantListPacket);
    }
}
