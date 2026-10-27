using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;

using Sanctuary.Core.Extensions;

using Sanctuary.Game.Entities;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

/// <summary>
/// Appearance a referee can change on a world NPC or prop beyond its saved columns, stored as JSON in
/// <c>Properties</c> (DbWorldNpc and DbWorldFixture). Props save their name, animation and nameplate here too,
/// since their own row only has scale, tint and collision.
/// </summary>
public sealed class NpcProperties
{
    public string? Name { get; set; }
    public int? NameId { get; set; }
    public int? ModelId { get; set; }
    public int? Animation { get; set; }
    public int? Disposition { get; set; }
    public bool? HideNamePlate { get; set; }

    public string? TextureAlias { get; set; }
    public string? TintAlias { get; set; }
    public int? TintId { get; set; }
    public int? CompositeEffectId { get; set; }
    public int? SubTextNameId { get; set; }
    public int? NameplateImageId { get; set; }
    public float? VerticalOffset { get; set; }
    public int? ImageSetId { get; set; }

    /// <summary>
    /// Tilt in radians (see <see cref="Rotate"/>); the heading is the saved spawn rotation's.
    /// </summary>
    public float? Pitch { get; set; }
    public float? Roll { get; set; }

    /// <summary>
    /// Overhead icons (quest, Station Cash, festive, ...). Null keeps the NPC definition's icons.
    /// </summary>
    public List<NpcIcon>? Icons { get; set; }

    public sealed record NpcIcon(int Type, int IconId, int State);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string Capture(Npc npc, bool includeBasics)
    {
        var properties = new NpcProperties
        {
            TextureAlias = npc.TextureAlias,
            TintAlias = npc.TintAlias,
            // A prop's tint is its row's Tint column.
            TintId = includeBasics ? null : npc.TintId,
            CompositeEffectId = npc.CompositeEffectId,
            SubTextNameId = npc.SubTextNameId,
            NameplateImageId = npc.NameplateImageId,
            VerticalOffset = npc.VerticalOffset,
            ImageSetId = npc.ImageSetId,
            Pitch = npc.Tilt is { Y: not 0f } tilt ? tilt.Y : null,
            Roll = npc.Tilt is { Z: not 0f } tiltRoll ? tiltRoll.Z : null,
            Icons = GetIcons(npc)
        };

        if (includeBasics)
        {
            properties.Name = npc.Name;
            properties.NameId = npc.NameId;
            properties.ModelId = npc.ModelId;
            properties.Animation = npc.Animation;
            properties.Disposition = npc.Disposition;
            properties.HideNamePlate = npc.HideNamePlate;
        }

        return JsonSerializer.Serialize(properties, JsonOptions);
    }

    public static void Apply(Npc npc, string? json)
    {
        if (string.IsNullOrEmpty(json))
            return;

        NpcProperties? properties;

        try
        {
            properties = JsonSerializer.Deserialize<NpcProperties>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return;
        }

        if (properties is null)
            return;

        if (properties.NameId.HasValue)
        {
            npc.Name = properties.Name;
            npc.NameId = properties.NameId.Value;
        }

        npc.ModelId = properties.ModelId ?? npc.ModelId;
        npc.Animation = properties.Animation ?? npc.Animation;
        npc.Disposition = properties.Disposition ?? npc.Disposition;
        npc.HideNamePlate = properties.HideNamePlate ?? npc.HideNamePlate;

        npc.TextureAlias = properties.TextureAlias ?? npc.TextureAlias;
        npc.TintAlias = properties.TintAlias ?? npc.TintAlias;
        npc.TintId = properties.TintId ?? npc.TintId;
        npc.CompositeEffectId = properties.CompositeEffectId ?? npc.CompositeEffectId;
        npc.SubTextNameId = properties.SubTextNameId ?? npc.SubTextNameId;
        npc.NameplateImageId = properties.NameplateImageId ?? npc.NameplateImageId;
        npc.VerticalOffset = properties.VerticalOffset ?? npc.VerticalOffset;
        npc.ImageSetId = properties.ImageSetId ?? npc.ImageSetId;

        if (properties.Icons is not null)
            SetIcons(npc, properties.Icons);

        if (properties.Pitch is not null || properties.Roll is not null)
            npc.Tilt = new Vector4(GetHeading(npc.SpawnRotation), properties.Pitch ?? 0f, properties.Roll ?? 0f, 0f);
    }

    /// <summary>
    /// Heading (radians, the <see cref="QuaternionExtensions.FromHeading"/> convention) of an NPC rotation.
    /// </summary>
    public static float GetHeading(Quaternion rotation)
    {
        // NPC rotations are usually a flat facing direction (X/Z with W = 0).
        if (rotation.W == 0f && rotation.Y == 0f)
            return MathF.Atan2(rotation.X, rotation.Z);

        var forward = Vector3.Transform(Vector3.UnitZ, Quaternion.Normalize(rotation));
        return MathF.Atan2(forward.X, forward.Z);
    }

    /// <summary>
    /// Turns the NPC or prop about its yaw, pitch or roll axis ('y', 'p', 'r'), or levels it ('0').
    /// Pitch and roll are sent as a tilt, which makes it a placed object, as the zone's own tilted props are.
    /// </summary>
    public static void Rotate(Npc npc, char axis, float degrees)
    {
        var radians = degrees * MathF.PI / 180f;

        var heading = GetHeading(npc.Rotation);
        var pitch = npc.Tilt?.Y ?? 0f;
        var roll = npc.Tilt?.Z ?? 0f;

        switch (axis)
        {
            case 'y':
                heading += radians;
                break;
            case 'p':
                pitch = Wrap(pitch + radians);
                break;
            case 'r':
                roll = Wrap(roll + radians);
                break;
            default:
                pitch = roll = 0f;
                break;
        }

        var rotation = QuaternionExtensions.FromHeading(heading);

        Change(npc, () =>
        {
            // Props and furniture stay placed objects when level (see HousingZone.GetFixtureTilt).
            npc.Tilt = pitch == 0f && roll == 0f && !npc.IsWorldProp && !npc.IsHouseFixture ? null : new Vector4(heading, pitch, roll, 0f);
            npc.SpawnRotation = rotation;
            npc.UpdatePosition(npc.Position, rotation);
        });
    }

    private static float Wrap(float radians)
    {
        radians %= 2f * MathF.PI;

        // Snap a full turn back to level, so the tilt can be dropped.
        return MathF.Abs(radians) < 1e-4f || MathF.Abs(MathF.Abs(radians) - 2f * MathF.PI) < 1e-4f ? 0f : radians;
    }

    public static List<NpcIcon> GetIcons(Npc npc)
    {
        var icons = npc.Notifications.Select(x => new NpcIcon(x.NotificationType, x.IconId, x.IconState)).ToList();

        if (npc.Notification is { } notification)
            icons.Add(new NpcIcon(notification.NotificationType, notification.IconId, notification.IconState));

        return icons;
    }

    public static void SetIcons(Npc npc, IEnumerable<NpcIcon> icons)
    {
        npc.Notification = null;
        npc.Notifications = [.. icons.Select(icon => InteractionMenuHelper.GetNotification(npc.Guid, new NotificationInfo
        {
            NotificationType = icon.Type,
            IconId = icon.IconId,
            IconState = icon.State
        }))];
    }

    /// <summary>
    /// Applies an appearance change and re-sends the NPC to everyone who sees it. The NPC is removed first,
    /// so icons it had before the change are taken down too.
    /// </summary>
    public static void Change(Npc npc, Action change)
    {
        var viewers = npc.VisiblePlayers.Values.ToList();

        foreach (var viewer in viewers)
            viewer.OnRemoveVisibleNpcs([npc]);

        change();

        foreach (var viewer in viewers)
            viewer.OnAddVisibleNpcs([npc]);
    }
}
