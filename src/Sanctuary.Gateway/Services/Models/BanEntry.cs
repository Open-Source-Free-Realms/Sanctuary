using System;
using System.Collections.Generic;

namespace Sanctuary.Gateway.Services.Models;

public sealed class BanEntry
{
    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public List<string> CharacterNames { get; set; } = new();
    public List<string> KnownIps { get; set; } = new(); // <-- FIXED
    public string Reason { get; set; } = string.Empty;
    public string BannedBy { get; set; } = string.Empty;
    public DateTime BannedAtUtc { get; set; }
}