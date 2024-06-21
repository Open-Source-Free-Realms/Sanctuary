using System.Diagnostics;
using System.Collections.Concurrent;

using Sanctuary.Game.Entities;

namespace Sanctuary.Game;

[DebuggerDisplay("{Longitude}, {Latitude}")]
public class ZoneTile
{
    public int Longitude;
    public int Latitude;

    public ConcurrentBag<IEntity> Entities = new();

    private ZoneTile()
    {
    }

    public ZoneTile(int longitude, int latitude)
    {
        Longitude = longitude;
        Latitude = latitude;
    }
}