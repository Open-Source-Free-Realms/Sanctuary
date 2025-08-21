﻿using System.Numerics;
using System.Text.Json.Serialization;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class PointOfInterestDefinition : ISerializableType
{
    public int Id { get; set; }
    public int NameId { get; set; }

    public int LocationId { get; set; }

    [JsonConverter(typeof(Vector4JsonConverter))]
    public Vector4 Position { get; set; }

    [JsonConverter(typeof(Vector4JsonConverter))]
    public Vector4 SpawnPosition { get; set; }

    public float Heading { get; set; }

    public int IconId { get; set; }

    public int NotificationType { get; set; }

    public int SubNameId { get; set; }

    public int Unknown { get; set; }

    public int BreadcrumbQuestId { get; set; }
    public int TeleportLocationId { get; set; }

    public int Unknown2 { get; set; }

    public string AtlasName { get; set; } = null!;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);

        writer.Write(NameId);

        writer.Write(LocationId);

        writer.Write(Position.X);
        writer.Write(Position.Y);
        writer.Write(Position.Z);

        writer.Write(Heading);

        writer.Write(IconId);

        writer.Write(NotificationType);

        writer.Write(SubNameId);

        writer.Write(Unknown);

        writer.Write(BreadcrumbQuestId);

        writer.Write(TeleportLocationId);

        writer.Write(AtlasName);

        writer.Write(Unknown2);
    }
}