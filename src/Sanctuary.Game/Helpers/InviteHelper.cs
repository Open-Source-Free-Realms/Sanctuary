namespace Sanctuary.Game.Helpers;

public static class InviteHelper
{
    // The client drops an invite or request after 300 seconds and answers "timed out" itself; the extra 10 let that answer arrive first.
    public const double ExpirySeconds = 310;
}
