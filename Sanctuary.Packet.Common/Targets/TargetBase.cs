using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common.Targets;

public class TargetBase : ISerializableType
{
    public Vector4 Unknown { get; }

    public virtual void Serialize(PacketWriter writer)
    {
        writer.Write(Unknown);
    }
}