using System.Numerics;
using System.Text.Json.Serialization;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class BoundingBox : ISerializableType
{
    [JsonConverter(typeof(Vector4JsonConverter))]
    public Vector4 Min { get;set; }

    [JsonConverter(typeof(Vector4JsonConverter))]
    public Vector4 Max { get; set; }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Min);
        writer.Write(Max);
    }
}