using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Game.Resources.Definitions;

public class ClientItemStatDefinition : BaseItemStatDefinition
{
    public object? Value;

    // Hide from UI?
    public bool Unknown;

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