using System;
using System.IO;
using System.IO.Compression;
using System.Text;

using DotRecast.Detour;
using DotRecast.Detour.Io;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Game.Pathfinding;

public static class NavMeshLoader
{
    private const string Signature = "SNAV";
    private const uint SupportedVersion = 1;

    public static bool TryLoad(string filePath, ILogger logger, out DtNavMesh? navMesh)
    {
        navMesh = null;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(stream);

            if (Encoding.ASCII.GetString(reader.ReadBytes(Signature.Length)) != Signature)
            {
                logger.LogError("Navmesh file \"{Path}\" has an invalid signature.", filePath);
                return false;
            }

            var version = reader.ReadUInt32();
            if (version != SupportedVersion)
            {
                logger.LogError("Navmesh file \"{Path}\" has unsupported version {Version}.", filePath, version);
                return false;
            }

            using var zLibStream = new ZLibStream(stream, CompressionMode.Decompress);
            using var meshReader = new BinaryReader(zLibStream);

            navMesh = new DtMeshSetReader().Read(meshReader);
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            logger.LogError(exception, "Failed to load navmesh file \"{Path}\".", filePath);
            return false;
        }
    }
}
