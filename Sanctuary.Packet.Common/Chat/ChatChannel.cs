namespace Sanctuary.Packet.Common.Chat;

public enum ChatChannel : short
{
    Say,
    Tell,
    System,
    Whisper,
    Group,
    Shout,
    Trade,
    Lfg, // Looking for group
    Area,
    Guild,
    Member,
}