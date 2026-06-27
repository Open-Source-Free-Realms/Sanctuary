namespace Sanctuary.Core.Configuration;

public sealed class NameFilterOptions
{
    public const string Section = "NameFilter";

    public string[] BlockedSubstrings { get; set; } = [];
}
