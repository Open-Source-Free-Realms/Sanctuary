using System;
using System.Text.Json.Serialization;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ClientItemStatDefinition : BaseItemStatDefinition
{
    [JsonConverter(typeof(StatConverter))]
    public object? Value { get; set; }

    // Hide from UI?
    public bool Unknown { get; set; }

    public override void Serialize(PacketWriter writer)
    {
        base.Serialize(writer);

        if (Type == 0)
            writer.Write(Convert.ToInt32(Value));
        else if(Type == 1)
            writer.Write(Convert.ToSingle(Value));

        writer.Write(Unknown);
    }
}