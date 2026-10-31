using System;

namespace Sanctuary.Game.Groups;

public sealed record PendingGroupInvite(ulong InviterGuid, DateTimeOffset ExpiresAt);
