using System.Collections.Generic;

using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Resources.Definitions.Zones;

public sealed class HousingZoneDefinition : BaseZoneDefinition
{
    public required string DisplayName { get; set; }
    public required string CommandName { get; set; }

    public int NameId { get; set; }
    public int IconId { get; set; }

    public int StoreBundleId { get; set; }
    public int ItemDefinitionId { get; set; }

    public required string DirectorySnapshot { get; set; }
    public List<BoundingBox> BuildAreas { get; set; } = [];
}
