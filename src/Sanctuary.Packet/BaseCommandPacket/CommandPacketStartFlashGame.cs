using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Loads a Flash minigame's movie through a Lua loader script (MiniGameFlash).
/// </summary>
public class CommandPacketStartFlashGame : BaseCommandPacket, ISerializablePacket
{
    public new const short OpCode = 0xC;

    public string LoaderScriptName = "MiniGameFlash";
    public string GameSwfName = string.Empty;

    /// <summary>
    /// Return to the Games window when the game closes, instead of showing the end score screen.
    /// </summary>
    public bool ReturnToPortal;

    public CommandPacketStartFlashGame() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        writer.Write(LoaderScriptName);
        writer.Write(GameSwfName);
        writer.Write(ReturnToPortal);

        return writer.Buffer;
    }
}
