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
        throw new NotImplementedException();
    }
}

public class QuaternionJsonConverter : JsonConverter<Quaternion>
{
    public override Quaternion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = JsonSerializer.Deserialize<float[]>(ref reader, options);

        if (list?.Length == 4)
            return new Quaternion(list[0], list[1], list[2], list[3]);

        return Quaternion.Zero;
    }

    public override void Write(Utf8JsonWriter writer, Quaternion value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}