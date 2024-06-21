using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class PacketLoadWelcomeScreen : ISerializablePacket
{
    public const short OpCode = 93;

    /// <summary>
    /// This has to be sent to true or the client won't render the rest of the UI.
    /// </summary>
    public bool ShowWelcomeScreen = true;

    public List<ContentInfo> Contents = new();
    public List<ClaimCodeInfo> ClaimCodes = new();

    public int SecondsSinceLastLogin;
    public int StartingScWalletBalance;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(ShowWelcomeScreen);

        writer.Write(Contents);
        writer.Write(ClaimCodes);

        writer.Write(SecondsSinceLastLogin);
        writer.Write(StartingScWalletBalance);

        return writer.Buffer;
    }
}