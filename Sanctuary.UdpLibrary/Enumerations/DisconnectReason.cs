namespace Sanctuary.UdpLibrary.Enumerations;

public enum DisconnectReason : short
{
    None,
    IcmpError,
    Timeout,
    OtherSideTerminated,
    ManagerDeleted,
    ConnectFail,
    Application,
    UnreachableConnection,
    UnacknowledgedTimeout,
    NewConnectionAttempt,
    ConnectionRefused,
    MutualConnectError,
    ConnectingToSelf,
    ReliableOverflow,
    ApplicationReleased,
    CorruptPacket,
    OtherProtocolName
}