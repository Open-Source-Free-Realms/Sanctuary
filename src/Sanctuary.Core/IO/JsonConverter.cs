using System;
using System.Numerics;
using System.Text.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sanctuary.Core.IO;

public class Vector4JsonConverter : JsonConverter<Vector4>
{
    public override Vector4 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = JsonSerializer.Deserialize<List<float>>(ref reader, options);

        if (list?.Count != 4)
            return Vector4.Zero;

        return new Vector4(list[0], list[1], list[2], list[3]);
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
        var list = JsonSerializer.Deserialize<List<float>>(ref reader, options);

        if (list?.Count != 4)
            return Quaternion.Zero;

        return new Quaternion(list[0], list[1], list[2], list[3]);
    }

    public override void Write(Utf8JsonWriter writer, Quaternion value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}