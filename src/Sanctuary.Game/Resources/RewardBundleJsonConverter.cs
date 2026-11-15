using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Resources;

public class RewardBundleJsonConverter : JsonConverter<RewardBundleBase>
{
    private const string ItemsPropertyName = "Items";

    public override RewardBundleBase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var jsonDocument = JsonDocument.ParseValue(ref reader);

        var rewardBundle = RewardBundleBase.CreateEmpty();

        foreach (var property in jsonDocument.RootElement.EnumerateObject())
        {
            if (!property.NameEquals(ItemsPropertyName))
            {
                SetField(rewardBundle, property, options);
                continue;
            }

            foreach (var item in property.Value.EnumerateArray())
            {
                var rewardBundleEntryItem = new RewardBundleEntryItem();

                foreach (var itemProperty in item.EnumerateObject())
                    SetField(rewardBundleEntryItem, itemProperty, options);

                rewardBundle.Entries.Add(rewardBundleEntryItem);
            }
        }

        return rewardBundle;
    }

    public override void Write(Utf8JsonWriter writer, RewardBundleBase value, JsonSerializerOptions options)
    {
        throw new NotSupportedException();
    }

    private static void SetField(object target, JsonProperty property, JsonSerializerOptions options)
    {
        var field = target.GetType().GetField(property.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? throw new JsonException($"Unknown reward bundle field \"{property.Name}\".");

        field.SetValue(target, property.Value.Deserialize(field.FieldType, options));
    }
}
