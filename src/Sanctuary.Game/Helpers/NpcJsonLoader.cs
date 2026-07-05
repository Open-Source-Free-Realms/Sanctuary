using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.Helpers;

public static class NpcJsonLoader
{
    public static int LoadIntoZone(IZone zone, string path, ulong spawnedByGuid = 0, int count = int.MaxValue, int offset = 0)
    {
        if (!File.Exists(path))
            return 0;

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return 0;

        var imported = 0;
        var index = 0;

        foreach (var npcData in doc.RootElement.EnumerateArray())
        {
            if (index++ < offset)
                continue;

            if (imported >= count)
                break;

            if (!zone.TryCreateNpc(out var npc))
                continue;

            npc.Visible = true;
            npc.IsInteractable = true;

            if (TryGetInt(npcData, "NameId", out var nameId))
                npc.NameId = nameId;

            if (TryGetInt(npcData, "ModelId", out var modelId))
                npc.ModelId = modelId;
            else if (TryGetInt(npcData, "Model Id", out var modelId2))
                npc.ModelId = modelId2;

            if (TryGetString(npcData, "Name", out var name))
                npc.Name = name;

            if (TryGetInt(npcData, "SubTextNameId", out var subTextNameId))
                npc.SubTextNameId = subTextNameId;

            if (TryGetBool(npcData, "HideNamePlate", out var hideNamePlate))
                npc.HideNamePlate = hideNamePlate;

            if (TryGetInt(npcData, "NameplateImageId", out var nameplateImageId))
                npc.NameplateImageId = nameplateImageId;

            if (TryGetFloat(npcData, "VerticalOffset", out var verticalOffset))
                npc.VerticalOffset = verticalOffset;

            if (TryGetString(npcData, "TextureAlias", out var textureAlias))
                npc.TextureAlias = textureAlias;
            else if (TryGetString(npcData, "Texture Alias", out var textureAlias2))
                npc.TextureAlias = textureAlias2;

            if (TryGetFloat(npcData, "Scale", out var scale))
                npc.Scale = scale <= 0f ? 1.0f : scale;
            else
                npc.Scale = 1.0f;

            if (TryGetInt(npcData, "InteractRange", out var interactRange))
                npc.InteractRange = interactRange;

            if (TryGetInt(npcData, "AreaDefinitionId", out var areaDefinitionId))
                npc.AreaDefinitionId = areaDefinitionId;

            if (TryGetInt(npcData, "ImageSetId", out var imageSetId))
                npc.ImageSetId = imageSetId;

            if (TryGetInt(npcData, "CursorId", out var cursorId))
                npc.CursorId = (byte)cursorId;

            npc.IsCommandSpawned = true;
            npc.SpawnedByGuid = spawnedByGuid;
            npc.CreatedAtUtc = DateTime.UtcNow;

            var px = GetRequiredFloat(npcData, "PositionX", "Position X");
            var py = GetRequiredFloat(npcData, "PositionY", "Position Y");
            var pz = GetRequiredFloat(npcData, "PositionZ", "Position Z");

            var rx = GetOptionalFloat(npcData, 0f, "RotationX", "Rotation X");
            var ry = GetOptionalFloat(npcData, 0f, "RotationY", "Rotation Y");
            var rz = GetOptionalFloat(npcData, 0f, "RotationZ", "Rotation Z");
            var rw = GetOptionalFloat(npcData, 1f, "RotationW", "Rotation W");

            npc.UpdatePosition(
                new Vector4(px, py, pz, 1f),
                new Quaternion(rx, ry, rz, rw)
            );

            if (npc.ZoneTile == ZoneTile.Empty)
            {
                zone.TryRemoveNpc(npc.Guid);
                continue;
            }

            imported++;
        }

        return imported;
    }

    public static int SaveCommandSpawnedFromZone(IZone zone, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var npcs = zone.Npcs
            .Where(n => n.IsCommandSpawned)
            .Select(n => new
            {
                n.NameId,
                n.Name,
                n.ModelId,
                n.TextureAlias,
                PositionX = n.Position.X,
                PositionY = n.Position.Y,
                PositionZ = n.Position.Z,
                RotationX = n.Rotation.X,
                RotationY = n.Rotation.Y,
                RotationZ = n.Rotation.Z,
                RotationW = n.Rotation.W,
                n.Scale
            })
            .ToList();

        var json = JsonSerializer.Serialize(npcs, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(path, json);
        return npcs.Count;
    }

    private static bool TryGetInt(JsonElement element, string propertyName, out int value)
    {
        value = default;

        if (!element.TryGetProperty(propertyName, out var prop))
            return false;

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out value))
            return true;

        if (prop.ValueKind == JsonValueKind.String &&
            int.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return true;

        return false;
    }

    private static bool TryGetFloat(JsonElement element, string propertyName, out float value)
    {
        value = default;

        if (!element.TryGetProperty(propertyName, out var prop))
            return false;

        return TryReadFloat(prop, out value);
    }

    private static bool TryGetBool(JsonElement element, string propertyName, out bool value)
    {
        value = default;

        if (!element.TryGetProperty(propertyName, out var prop))
            return false;

        if (prop.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = prop.GetBoolean();
            return true;
        }

        if (prop.ValueKind == JsonValueKind.String &&
            bool.TryParse(prop.GetString(), out value))
        {
            return true;
        }

        return false;
    }

    private static float GetRequiredFloat(JsonElement element, string primaryName, string fallbackName)
    {
        if (element.TryGetProperty(primaryName, out var primary) && TryReadFloat(primary, out var primaryValue))
            return primaryValue;

        if (element.TryGetProperty(fallbackName, out var fallback) && TryReadFloat(fallback, out var fallbackValue))
            return fallbackValue;

        throw new InvalidDataException($"Missing required float property '{primaryName}' or '{fallbackName}'.");
    }

    private static float GetOptionalFloat(JsonElement element, float defaultValue, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var prop) && TryReadFloat(prop, out var value))
                return value;
        }

        return defaultValue;
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        value = null;

        if (!element.TryGetProperty(propertyName, out var prop))
            return false;

        if (prop.ValueKind == JsonValueKind.Null)
        {
            value = null;
            return true;
        }

        if (prop.ValueKind != JsonValueKind.String)
            return false;

        value = prop.GetString();
        return true;
    }

    private static bool TryReadFloat(JsonElement prop, out float value)
    {
        value = default;

        if (prop.ValueKind == JsonValueKind.Number)
        {
            value = prop.GetSingle();
            return true;
        }

        if (prop.ValueKind == JsonValueKind.String &&
            float.TryParse(prop.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;

        return false;
    }
}
