using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BaseActivityPacket : BaseActivityServicePacket
{
    public new const byte OpCode = 1;

    private byte SubOpCode;

    public BaseActivityPacket(byte subOpCode) : base(OpCode)
    {
        SubOpCode = subOpCode;
    }

    public override void Write(PacketWriter writer)
    {
        base.Write(writer);

        writer.Write(SubOpCode);
    }

    public override bool TryRead(ref PacketReader reader)
    {
        if (!base.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out byte subOpCode) && subOpCode != SubOpCode)
            return false;

        return true;
    }
}