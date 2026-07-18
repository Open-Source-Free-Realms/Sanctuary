using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Sanctuary.Core.IO;

public ref struct PacketReader
{
    private int position;
    private readonly int length;
    private readonly ref byte reference;

    public PacketReader(ReadOnlySpan<byte> span)
    {
        length = span.Length;
        reference = ref MemoryMarshal.GetReference(span);
    }

    public readonly int RemainingLength => length - position;

    public readonly ReadOnlySpan<byte> Span => MemoryMarshal.CreateReadOnlySpan(ref reference, length);

    public readonly ReadOnlySpan<byte> ConsumedSpan => MemoryMarshal.CreateReadOnlySpan(ref reference, position);

    public readonly ReadOnlySpan<byte> RemainingSpan => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref reference, position), RemainingLength);

    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)count, (uint)RemainingLength, nameof(count));

        position += count;
    }

    public void Rewind(int count)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)count, (uint)position, nameof(count));

        position -= count;
    }

    public void Reset() => position = 0;

    public ReadOnlySpan<byte> Read(int count)
    {
        if (!TryRead(count, out var result))
            throw new InternalBufferOverflowException();

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe T Read<T>() where T : unmanaged
    {
        return Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(Read(sizeof(T))));
    }

    private bool TryRead(int count, out ReadOnlySpan<byte> result)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var newLength = position + count;

        if ((uint)newLength <= (uint)length)
        {
            result = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref reference, position), count);
            position = newLength;
            return true;
        }

        result = default;

        return false;
    }

    public unsafe bool TryRead<T>(out T result) where T : unmanaged
    {
        if (MemoryMarshal.TryRead(RemainingSpan, out result))
        {
            Advance(sizeof(T));
            return true;
        }

        return false;
    }

    public bool TryReadInt16(out short result, bool isLittleEndian = false)
    {
        if (!TryRead(out result))
            return false;

        if (isLittleEndian != BitConverter.IsLittleEndian)
            result = BinaryPrimitives.ReverseEndianness(result);

        return true;
    }

    public bool TryReadUInt16(out ushort result, bool isLittleEndian = false)
    {
        if (!TryRead(out result))
            return false;

        if (isLittleEndian != BitConverter.IsLittleEndian)
            result = BinaryPrimitives.ReverseEndianness(result);

        return true;
    }

    public bool TryReadInt32(out int result, bool isLittleEndian = false)
    {
        if (!TryRead(out result))
            return false;

        if (isLittleEndian != BitConverter.IsLittleEndian)
            result = BinaryPrimitives.ReverseEndianness(result);

        return true;
    }

    public bool TryReadUInt32(out uint result, bool isLittleEndian = false)
    {
        if (!TryRead(out result))
            return false;

        if (isLittleEndian != BitConverter.IsLittleEndian)
            result = BinaryPrimitives.ReverseEndianness(result);

        return true;
    }

    public bool TryReadInt64(out long result, bool isLittleEndian = false)
    {
        if (!TryRead(out result))
            return false;

        if (isLittleEndian != BitConverter.IsLittleEndian)
            result = BinaryPrimitives.ReverseEndianness(result);

        return true;
    }

    public bool TryRead<T>(out List<T> value) where T : unmanaged
    {
        value = [];

        if (!TryRead(out int count))
            return false;

        for (int i = 0; i < count; i++)
        {
            if (!TryRead(out T item))
                return false;

            value.Add(item);
        }

        return true;
    }

    public bool TryReadList<T>(out List<T> value) where T : IDeserializableType, new()
    {
        value = [];

        if (!TryRead(out int count))
            return false;

        for (int i = 0; i < count; i++)
        {
            var item = new T();

            if (!item.TryRead(ref this))
                return false;

            value.Add(item);
        }

        return true;
    }

    public bool TryRead(out string value)
    {
        if (!TryRead(out int length))
        {
            value = string.Empty;
            return false;
        }

        if (length == 0)
        {
            value = string.Empty;
            return true;
        }

        if (!TryRead(length, out var stringSequence))
        {
            value = string.Empty;
            return false;
        }

        value = Encoding.UTF8.GetString(stringSequence);

        return true;
    }

    public bool TryReadExact(int count, out Span<byte> result)
    {
        if (count >= 0)
        {
            var span = RemainingSpan;

            if ((uint)count <= (uint)span.Length)
            {
                result = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(span), count);

                Advance(count);

                return true;
            }
        }

        result = default;

        return false;
    }

    public bool TryRead(out Vector4 value, bool limited = false)
    {
        value = new Vector4();

        if (!TryRead(out value.X))
            return false;

        if (!TryRead(out value.Y))
            return false;

        if (!TryRead(out value.Z))
            return false;

        if (limited)
            value.W = 1.0f;
        else if (!TryRead(out value.W))
            return false;

        return true;
    }

    public bool TryRead(out Quaternion value, bool limited = false)
    {
        value = new Quaternion();

        if (!TryRead(out value.X))
            return false;

        if (!TryRead(out value.Y))
            return false;

        if (!TryRead(out value.Z))
            return false;

        if (limited)
            value.W = 0.0f;
        else if (!TryRead(out value.W))
            return false;

        return true;
    }
}