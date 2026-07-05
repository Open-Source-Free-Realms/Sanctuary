using System;
using System.Collections.Generic;

namespace Sanctuary.Gateway.Services.Models;

public sealed class IpHistoryEntry
{
    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public List<string> CharacterNames { get; set; } = new();
    public List<string> KnownIps { get; set; } = new();
    public DateTime LastSeenUtc { get; set; }
}