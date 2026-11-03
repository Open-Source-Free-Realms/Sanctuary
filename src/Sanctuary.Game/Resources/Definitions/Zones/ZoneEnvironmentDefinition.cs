using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions.Zones;

public sealed class ZoneEnvironmentDefinition
{
    public const int OutOfBoundsEnvironmentId = -1;

    public Dictionary<int, string> Environments { get; set; } = [];

    public int StartLongitude { get; set; }
    public int StartLatitude { get; set; }

    public List<List<int>> Rows { get; set; } = [];

    public int GetEnvironmentId(int longitude, int latitude)
    {
        if (longitude < StartLongitude || longitude >= StartLongitude + Rows.Count)
            return OutOfBoundsEnvironmentId;

        var row = Rows[longitude - StartLongitude];

        if (latitude < StartLatitude || latitude >= StartLatitude + row.Count)
            return OutOfBoundsEnvironmentId;

        return row[latitude - StartLatitude];
    }
}
