using System;

namespace Sanctuary.Core.Cryptography;

public sealed class RC4
{
    private const int StateLength = 256;

    private int x, y;
    private byte[] engineState;

    public RC4(string key)
    {
        var keyBytes = Convert.FromBase64String(key);

        engineState = new byte[StateLength];

        for (var i = 0; i < StateLength; i++)
            engineState[i] = (byte)i;

        var i1 = 0;
        var i2 = 0;

        for (var i = 0; i < StateLength; i++)
        {
            i2 = ((keyBytes[i1] & 0xff) + engineState[i] + i2) & 0xff;

            var tmp = engineState[i];

            engineState[i] = engineState[i2];

            engineState[i2] = tmp;

            i1 = (i1 + 1) % keyBytes.Length;
        }
    }

    public void Apply(Span<byte> data)
    {
        for (var i = 0; i < data.Length; i++)
        {
            x = (x + 1) & 0xff;
            y = (engineState[x] + y) & 0xff;

            var sx = engineState[x];
            var sy = engineState[y];

            engineState[x] = sy;
            engineState[y] = sx;

            data[i] ^= engineState[(sx + sy) & 0xff];
        }
    }
}