namespace Sanctuary.Game;

public interface IZoneManager
{
    IZone StartingZone { get; }

    IZone? Get(int zoneId);

    bool LoadZones();
}