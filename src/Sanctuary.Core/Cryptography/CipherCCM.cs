using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;

using Sanctuary.Core.IO;

namespace Sanctuary.Core.Cryptography;

public sealed class CipherCCM : ICipher
{
    private AesCcm? _aesCcm;
    private ulong _encryptCounter;
    private ulong _decryptCounter;

    private const int TagLength = 16;
    private const int NonceLength = 13;

    public bool IsInitialized { get; private set; }

    public bool Initialize(byte[] key)
    {
        _aesCcm = new AesCcm(key);

        IsInitialized = true;

        return true;
    }

    public int GetFinalLength(int length)
    {
        return length + TagLength + NonceLength;
    }

    public bool Decrypt(Span<byte> data, out int finalLength)
    {
        if (_aesCcm is null)
        {
            finalLength = 0;
            return false;
        }

        var nonce = data.Slice(data.Length - NonceLength, NonceLength);
        var tag = data.Slice(data.Length - TagLength - NonceLength, TagLength);

        if (!HasValidData(nonce, NonceLength))
        {
            finalLength = 0;
            return false;
        }

        if (!HasValidData(tag, TagLength))
        {
            finalLength = 0;
            return false;
        }

        BinaryPrimitives.TryReadUInt64LittleEndian(nonce, out var counter);

        if (counter <= _decryptCounter)
        {
            finalLength = 0;
            return false;
        }

        var buffer = data.Slice(0, data.Length - TagLength - NonceLength);

        try
        {
            _aesCcm.Decrypt(nonce, buffer, tag, buffer);
        }
        catch
        {
            finalLength = 0;
            return false;
        }

        _decryptCounter = counter;

        finalLength = buffer.Length;

        return true;
    }

    public bool Encrypt(Span<byte> sourceData, PacketWriter writer)
    {
        if (_aesCcm is null)
            return false;

        Span<byte> nonce = stackalloc byte[NonceLength];

        BinaryPrimitives.WriteUInt64LittleEndian(nonce, ++_encryptCounter);
        RandomNumberGenerator.Fill(nonce.Slice(sizeof(ulong)));

        var tag = RandomNumberGenerator.GetBytes(TagLength);

        var encryptedData = ArrayPool<byte>.Shared.Rent(sourceData.Length);

        try
        {
            _aesCcm.Encrypt(nonce, sourceData, encryptedData.AsSpan(0, sourceData.Length), tag);
            writer.Write(encryptedData.AsSpan(0, sourceData.Length));
        }
        catch
        {
            return false;
        }

        finally
        {
            ArrayPool<byte>.Shared.Return(encryptedData);
        }

        writer.Write(tag);
        writer.Write(nonce);

        return true;
    }

    private bool HasValidData(ReadOnlySpan<byte> data, int expectedLength)
    {
        if (data.Length != expectedLength)
            return false;

        foreach (var b in data)
        {
            if (b != 0x00)
                return true;
        }

        return false;
    }
}