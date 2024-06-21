namespace Sanctuary.Game.Resources.Definitions;

public class GzneDefinition
{
    public bool HideTerrain;

    public int ChunkSize;
    public int TileSize;

    public float WorldMin;
    public int Unknown;

    public int TilePerChunkCoordinate => ChunkSize / TileSize;

    /// <summary>
    /// X axies
    /// </summary>
    public int StartLongitude;
    public int EndLongitude;

    /// <summary>
    /// Z axies
    /// </summary>
    public int StartLatitude;
    public int EndLatitude;
}