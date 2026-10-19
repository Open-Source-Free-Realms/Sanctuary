using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class CommandPacketShowDialog : BaseCommandPacket, ISerializablePacket
{
    public new const short OpCode = 3;

    public sealed class Response
    {
        public int Id;
        public int ActionType;
        public int LabelTextId;
        public int Param1;
        public int Param2;
    }

    public int DialogueTextId;
    public int TitleTextId;
    public ulong NpcGuid;

    public float CameraFocusParam = 1f;

    public readonly List<Response> Responses = new();

    public CommandPacketShowDialog() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        writer.Write(DialogueTextId);
        writer.Write(TitleTextId);
        writer.Write(NpcGuid);
        writer.Write(false);
        writer.Write(CameraFocusParam);

        writer.Write(Responses.Count);
        foreach (var response in Responses)
        {
            writer.Write(response.Id);
            writer.Write(response.ActionType);
            writer.Write(response.LabelTextId);
            writer.Write(response.Param1);
            writer.Write(response.Param2);
        }

        for (int i = 0; i < 4; i++) writer.Write(0f);
        for (int i = 0; i < 4; i++) writer.Write(0f);
        writer.Write(false);
        for (int i = 0; i < 4; i++) writer.Write(0f);
        writer.Write(0f);
        writer.Write(false);
        writer.Write(false);
        writer.Write(false);
        writer.Write(0f);
        writer.Write(0);

        return writer.Buffer;
    }
}
