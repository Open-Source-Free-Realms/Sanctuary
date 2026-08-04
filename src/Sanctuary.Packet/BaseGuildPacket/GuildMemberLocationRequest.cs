namespace Sanctuary.Packet;

public class GuildMemberLocationRequest : BaseGuildPacket
{
    public new const short OpCode = 15;

    public GuildMemberLocationRequest() : base(OpCode)
    {
    }
}