namespace Sanctuary.Game.Helpers;

public static class InviteHelper
{
    // The client keeps an invite or request for five minutes, then answers "timed out" on its own.
    public const double ClientLifeSeconds = 300;

    // Lets the client's own answer arrive before the server gives up on it.
    public const double ExpiryMarginSeconds = 10;

    public const double ExpirySeconds = ClientLifeSeconds + ExpiryMarginSeconds;
}
