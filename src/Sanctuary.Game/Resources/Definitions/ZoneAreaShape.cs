using System.Text.Json.Serialization;

namespace Sanctuary.Game.Resources.Definitions;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ZoneAreaShape
{
    Circle,
    Rectangle,
    Polygon
}
