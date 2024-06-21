using System;

namespace Sanctuary.Packet.Common.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class PacketHandler : Attribute
{
    public const string ConfigureMethodName = "ConfigureServices";
}