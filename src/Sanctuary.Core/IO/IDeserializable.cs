using System;
using System.Diagnostics.CodeAnalysis;

namespace Sanctuary.Core.IO;

public interface IDeserializable<T>
{
    static abstract bool TryDeserialize(ReadOnlySpan<byte> data, out T value);
}