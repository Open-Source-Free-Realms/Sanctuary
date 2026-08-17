using System.Text.Json.Serialization;

namespace Sanctuary.Game.Resources.Definitions.Rewards;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CurrencyType
{
    Coins,
    StationCash
}
