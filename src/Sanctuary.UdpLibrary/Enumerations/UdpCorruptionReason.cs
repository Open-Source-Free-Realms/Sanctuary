namespace Sanctuary.UdpLibrary.Enumerations;

public enum UdpCorruptionReason
{
    None,
    MultiPacket,
    ReliablePacketTooShort,
    InternalPacketTooShort,
    DecryptFailed,
    ZeroLengthPacket,
    PacketShorterThanCrcBytes,
    MisformattedGroup,
    FragmentOversized,
    FragmentBad,
    FragmentExpected,
    AckBad
}