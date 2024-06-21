using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class BoundingBox : ISerializableType
{
    public Vector4 Min;
    public Vector4 Max;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Min);
        writer.Write(Max);
    }
}