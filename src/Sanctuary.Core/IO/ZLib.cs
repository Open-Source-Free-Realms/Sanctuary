using System;
using System.Buffers.Binary;

using ZLibDotNet;

namespace Sanctuary.Core.IO;

public static class ZLib
{
    private static readonly ZLibDotNet.ZLib _zlib = new();

    public static int Compress(Span<byte> input, Span<byte> output, bool prefixLength = false)
    {
        if (output.Length < 12)
            return -1;

        if (prefixLength)
        {
            output[0] = 0xA1;
            output[1] = 0xB2;
            output[2] = 0xC3;
            output[3] = 0xD4;

            BinaryPrimitives.WriteInt32BigEndian(output.Slice(4), input.Length);
        }

        ZStream zStream = new()
        {
            Input = input,
            Output = output,
            NextOut = prefixLength ? 8 : 0
        };

        if (_zlib.DeflateInit(ref zStream, 6) != ZLibDotNet.ZLib.Z_OK)
            return -1;

        if (_zlib.Deflate(ref zStream, ZLibDotNet.ZLib.Z_FINISH) != ZLibDotNet.ZLib.Z_STREAM_END)
            return -1;

        if (_zlib.DeflateEnd(ref zStream) != ZLibDotNet.ZLib.Z_OK)
            return -1;

        return zStream.NextOut + (prefixLength ? 8 : 0);
    }

    public static int Decompress(Span<byte> input, Span<byte> output)
    {
        if (input.Length < 1)
            return -1;

        var prefixedLength = GetPrefixedLength(input);

        if (prefixedLength >= 0)
        {
            if (output.Length < prefixedLength)
                return -1;

            input = input.Slice(8);
        }

        ZStream zStream = new()
        {
            Input = input,
            Output = output
        };

        if (_zlib.InflateInit(ref zStream) != ZLibDotNet.ZLib.Z_OK)
            return -1;

        if (_zlib.Inflate(ref zStream, ZLibDotNet.ZLib.Z_FINISH) != ZLibDotNet.ZLib.Z_STREAM_END)
            return -1;

        if (_zlib.InflateEnd(ref zStream) != ZLibDotNet.ZLib.Z_OK)
            return -1;

        return zStream.NextOut;
    }

    private static int GetPrefixedLength(Span<byte> input)
    {
        if (input.Length <= 8)
            return -1;

        var magic = BinaryPrimitives.ReadUInt32BigEndian(input);

        if (magic != 0xA1B2C3D4)
            return -1;

        var length = BinaryPrimitives.ReadInt32BigEndian(input.Slice(4));

        return length;
    }
}