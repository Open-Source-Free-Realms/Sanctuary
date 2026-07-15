using Sanctuary.Game.Entities;

namespace Sanctuary.Gateway.Handlers;

internal static class RefereeProfileAlias
{
    public static int ToStorageProfileId(Player player, int profileId)
    {
        return player.IsReferee && profileId == Player.RefereeProfileId
            ? player.ActiveProfileId
            : profileId;
    }

    public static int ToClientProfileId(Player player, int profileId)
    {
        return player.IsReferee && profileId == player.ActiveProfileId
            ? Player.RefereeProfileId
            : profileId;
    }
}
