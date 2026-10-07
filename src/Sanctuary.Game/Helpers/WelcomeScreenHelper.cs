using System;
using System.Collections.Generic;
using System.Linq;

using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.GameCommerce;

namespace Sanctuary.Game.Helpers;

public static class WelcomeScreenHelper
{
    public const int PopularItemsGroupId = 6;
    private const int StationCashCurrencyType = 1;
    private const int PopularItemCount = 8;

    public static StoreBundleGroupDefinition GetPopularItems(
        StoreBundleGroupDefinition template, IEnumerable<AppStoreBundleDefinition> catalog,
        int count = PopularItemCount, int currencyType = StationCashCurrencyType)
    {
        // Sample without replacement from the SC bundles actually sent to this client.
        var candidates = catalog.Where(x => x.CurrencyType == currencyType && x.NameId > 0
            && x.DescriptionId > 0 && int.TryParse(x.Image.Image, out var imageId) && imageId > 0)
            .DistinctBy(x => x.Id).ToArray();
        Random.Shared.Shuffle(candidates);

        return new StoreBundleGroupDefinition
        {
            Id = template.Id,
            NameId = template.NameId,
            DescriptionId = template.DescriptionId,
            Status = template.Status,
            Image = template.Image,
            IsExclusive = template.IsExclusive,
            Entries = candidates.Take(Math.Max(0, count)).Select((bundle, index) =>
                new StoreBundleGroupDefinition.Entry
                {
                    StoreBundleId = bundle.Id,
                    DisplayOrder = index + 1
                }).ToDictionary(x => x.StoreBundleId)
        };
    }

    // Match the client's default marketplace banner, with a real announcement row
    // so its carousel initializes the count and selected index.
    public static AnnouncementDataSendPacket GetAnnouncementsPacket(IEnumerable<AnnouncementInfo>? announcements = null) => new()
    {
        Announcements = announcements?.ToList() ??
        [
            new()
            {
                Id = 1,
                Priority = 1,
                IconId = 38443,
                TitleStringId = 414,
                BodyStringId = 415,
                ButtonStringId = 436323,
                LuaCall = "Marketplace",
                Param2 = 31415
            }
        ]
    };

    // The native welcome callback treats a negative value as the first-login flag.
    public static int SecondsSinceLastLogin(DateTimeOffset? previousLogin, DateTimeOffset loginTime) =>
        previousLogin.HasValue
            ? (int)Math.Clamp((loginTime - previousLogin.Value).TotalSeconds, 0, int.MaxValue)
            : -1;

    public static PacketLoadWelcomeScreen GetWelcomeScreenPacket(int secondsSinceLastLogin, int stationCash,
        IEnumerable<ContentInfo>? contents = null, IEnumerable<ClaimCodeInfo>? claimCodes = null)
    {
        var packetLoadWelcomeScreen = new PacketLoadWelcomeScreen
        {
            SecondsSinceLastLogin = secondsSinceLastLogin,
            StartingScWalletBalance = stationCash
        };

        packetLoadWelcomeScreen.Contents.AddRange(contents ??
        [
            new ContentInfo
            {
                NameId = 6185,
                DescriptionId = 6186,
            },
            new ContentInfo
            {
                NameId = 6187,
                DescriptionId = 6188,
            },
            new ContentInfo
            {
                NameId = 6189,
                DescriptionId = 6190,
            }
        ]);

        packetLoadWelcomeScreen.ClaimCodes.AddRange(claimCodes ??
        [
            new ClaimCodeInfo
            {
                Code = "MMMDONUT",
                NameId = 401519,
                DescriptionId = 401534,
                IconId = 929
            },
            new ClaimCodeInfo
            {
                Code = "BERRYCUPCAKE",
                NameId = 401517,
                DescriptionId = 401532,
                IconId = 939
            },
            new ClaimCodeInfo
            {
                Code = "SKELETAL",
                NameId = 409157,
                DescriptionId = 109132,
                IconId = 3459,
                TintAlias = "navy"
            },
            new ClaimCodeInfo
            {
                Code = "STRAWBERRIES",
                NameId = 409158,
                DescriptionId = 108948,
                IconId = 3441,
                TintAlias = "concrete"
            },
            new ClaimCodeInfo
            {
                Code = "FROGGY",
                NameId = 409159,
                DescriptionId = 3141,
                IconId = 1258
            },
            new ClaimCodeInfo
            {
                Code = "SANDWICH",
                NameId = 409160,
                DescriptionId = 2430,
                IconId = 949
            }
        ]);

        return packetLoadWelcomeScreen;
    }
}
