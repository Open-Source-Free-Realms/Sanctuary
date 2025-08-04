using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sanctuary.Core.IO;

public class Vector4JsonConverter : JsonConverter<Vector4>
{
    public override Vector4 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = JsonSerializer.Deserialize<float[]>(ref reader, options);

        if (list?.Length == 4)
            return new Vector4(list);
        else if (list?.Length == 3)
            return new Vector4(new Vector3(list), 1f);

        return Vector4.Zero;
    }

    public override void Write(Utf8JsonWriter writer, Vector4 value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteNumberValue(value.Z);

        if (value.W != 1f)
            writer.WriteNumberValue(value.W);

        writer.WriteEndArray();
    }
}

public class QuaternionJsonConverter : JsonConverter<Quaternion>
{
    public override Quaternion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = JsonSerializer.Deserialize<float[]>(ref reader, options);

        if (list?.Length == 4)
            return new Quaternion(list[0], list[1], list[2], list[3]);
        else if (list?.Length == 2)
            return new Quaternion(list[0], 0f, list[1], 0f);

        return Quaternion.Zero;
    }

    public override void Write(Utf8JsonWriter writer, Quaternion value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Z);

        writer.WriteEndArray();
    }
}

public class StatConverter : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TryGetSingle(out var value))
            return value;
        else if (reader.TryGetInt32(out var value2))
            return value2;

        return null;
    }

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        if (value is float floatValue)
            writer.WriteNumberValue(floatValue);
        else if (value is int intValue)
            writer.WriteNumberValue(intValue);

        throw new NotImplementedException();
    }
}