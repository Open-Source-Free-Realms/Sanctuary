using System;
using System.Numerics;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Resources.Definitions.Zones;

namespace Sanctuary.Game.Zones;

public sealed class WorldZone : BaseZone
{
    private readonly WorldZoneDefinition _zoneDefinition;

    public WorldZone(WorldZoneDefinition zoneDefinition, IServiceProvider serviceProvider)
        : base(zoneDefinition, serviceProvider)
    {
        _zoneDefinition = zoneDefinition;
    }

    public int GetZoneAreaId(Vector4 position)
    {
        foreach (var areaDefinition in _zoneDefinition.AreaDefinitions)
        {
            if (areaDefinition.Shape == "Circle")
            {
                var circle = new Vector3(areaDefinition.X1, 0, areaDefinition.Z1);

                if (position.IsInCircle(circle, areaDefinition.Radius))
                    return areaDefinition.Id;
            }
            else if (areaDefinition.Shape == "Rectangle")
            {
                var p1 = new Vector3(areaDefinition.X1, 0, areaDefinition.Z1);
                var p2 = new Vector3(areaDefinition.X2, 0, areaDefinition.Z2);

                if (position.IsInRectangle(p1, p2))
                    return areaDefinition.Id;
            }
            else
            {
                throw new NotImplementedException(nameof(areaDefinition.Shape));
            }
        }

        return 0;
    }
}
