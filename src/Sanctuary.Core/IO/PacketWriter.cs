using System;
using System.IO;
using System.Text;
using System.Numerics;
using System.Collections.Generic;
using System.Xml;

namespace Sanctuary.Core.IO;

public class PacketWriter : IDisposable
{
    private readonly MemoryStream _stream;
    private readonly BinaryWriter _writer;

    public byte[] Buffer => _stream.ToArray();

    public PacketWriter()
    {
        _stream = new MemoryStream();
        _writer = new BinaryWriter(_stream);
    }

    public void Write(byte value) => _writer.Write(value);
    public void Write(bool value) => _writer.Write(value);

    public void Write(short value) => _writer.Write(value);
    public void Write(ushort value) => _writer.Write(value);

    public void Write(int value) => _writer.Write(value);
    public void Write(uint value) => _writer.Write(value);

    public void Write(long value) => _writer.Write(value);
    public void Write(ulong value) => _writer.Write(value);

    public void Write(float value) => _writer.Write(value);
    public void Write(double value) => _writer.Write(value);

    public void Write(string? value, int maxLength = int.MaxValue)
    {
        if (string.IsNullOrEmpty(value))
        {
            _writer.Write(0);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(value);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(bytes.Length, maxLength);

        _writer.Write(bytes.Length);
        _writer.Write(bytes);
    }

    public void Write(byte[] value) => _writer.Write(value);
    public void Write(Span<byte> value) => _writer.Write(value);

    public void WritePayload(byte[] value)
    {
        _writer.Write(value.Length);

        if (value.Length > 0)
            _writer.Write(value);
    }

    #region Helpers

    public void Write(Vector4 value, bool limited = false)
    {
        _writer.Write(value.X);
        _writer.Write(value.Y);
        _writer.Write(value.Z);

        if (!limited)
            _writer.Write(value.W);
    }

    public void Write(Quaternion value, bool limited = false)
    {
        _writer.Write(value.X);
        _writer.Write(value.Y);
        _writer.Write(value.Z);

        if (!limited)
            _writer.Write(value.W);
    }

    public void Write(DateTime value)
    {
        _writer.Write(value.Subtract(DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerSecond);
    }

    public void Write(DateTimeOffset value)
    {
        _writer.Write(value.Subtract(DateTimeOffset.UnixEpoch).Ticks / TimeSpan.TicksPerSecond);
    }

    public void Write(Enum value)
    {
        var typeCode = value.GetTypeCode();
        var enumValue = Convert.ChangeType(value, typeCode);

        switch (typeCode)
        {
            case TypeCode.Byte:
                _writer.Write((byte)enumValue);
                break;

            case TypeCode.SByte:
                _writer.Write((sbyte)enumValue);
                break;

            case TypeCode.Int16:
                _writer.Write((short)enumValue);
                break;

            case TypeCode.UInt16:
                _writer.Write((ushort)enumValue);
                break;

            case TypeCode.Int32:
                _writer.Write((int)enumValue);
                break;

            case TypeCode.UInt32:
                _writer.Write((uint)enumValue);
                break;

            case TypeCode.Int64:
                _writer.Write((long)enumValue);
                break;

            case TypeCode.UInt64:
                _writer.Write((ulong)enumValue);
                break;

            default:
                throw new NotSupportedException();
        }
    }

    public void Write(IList<int> value)
    {
        _writer.Write(value.Count);

        foreach (var item in value)
            _writer.Write(item);
    }

    public void Write(IList<ulong> value)
    {
        _writer.Write(value.Count);

        foreach (var item in value)
            _writer.Write(item);
    }

    public void Write<T>(IList<T> value) where T : ISerializableType
    {
        _writer.Write(value.Count);

        foreach (var item in value)
            item.Serialize(this);
    }

    public void Write<TValue>(IDictionary<int, TValue> value) where TValue : ISerializableType
    {
        Write(value.Count);

        foreach (var item in value)
        {
            _writer.Write(item.Key);
            item.Value.Serialize(this);
        }
    }

    public void Write<TValue>(IDictionary<uint, TValue> value) where TValue : ISerializableType
    {
        Write(value.Count);

        foreach (var item in value)
        {
            _writer.Write(item.Key);
            item.Value.Serialize(this);
        }
    }

    public void Write<TKey, TValue>(IDictionary<TKey, TValue> value)
        where TKey : Enum
        where TValue : ISerializableType
    {
        Write(value.Count);

        foreach (var item in value)
        {
            Write(item.Key);
            item.Value.Serialize(this);
        }
    }

    #endregion

    public void Dispose()
    {
        _writer.Close();
    }
}