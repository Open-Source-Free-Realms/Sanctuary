using System.Numerics;
using System.Text.Json.Serialization;

using Sanctuary.Core.IO;

namespace Sanctuary.Game.Resources.Definitions.QuestGoals;

public sealed class ReachLocationGoal : QuestGoal
{
    // [x, y, z]. The proximity check is 2D, so the Y only feeds the map pin.
    [JsonConverter(typeof(Vector3XzJsonConverter))]
    public Vector3 ReachPosition { get; set; }

    public float ReachRadius { get; set; } = 12f;
}
