using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;

using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Resources.Definitions;

public class NpcDefinition
{
    public int Id { get; set; }

    public int NameId { get; set; }
    public int SubTextNameId { get; set; }
    public string? Name { get; set; }

    public int ModelId { get; set; }
    public string ModelFileName { get; set; } = null!;

    public string? TextureAlias { get; set; }
    public int TerrainObjectId { get; set; }
    public bool ReplaceTerrainObject { get; set; }
    public float? Scale { get; set; }
    public bool OpensAtlas { get; set; }
    public bool AutoSelectSingleInteraction { get; set; }
    public int InteractRange { get; set; } = 100;
    public bool IsInteractable { get; set; } = true;
    public byte CursorId { get; set; }
    public bool? HasCursor { get; set; }
    public bool RelevanceUnknown2 { get; set; }
    public InteractionList? InteractionList { get; set; }
    public bool InteractionUnknown { get; set; }
    public NotificationInfo? Notification { get; set; }
    public List<NotificationInfo> Notifications { get; set; } = [];
    public string? TintAlias { get; set; }
    public int TintId { get; set; }
    public int Disposition { get; set; } = 1;
    public string[]? Scripts { get; set; }
}
