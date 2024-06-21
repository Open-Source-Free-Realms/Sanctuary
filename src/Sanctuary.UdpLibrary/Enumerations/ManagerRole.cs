namespace Sanctuary.UdpLibrary.Enumerations;

public enum ManagerRole
{
    /// <summary>
    /// Original default UdpLibrary settings.
    /// </summary>
    Default,

    /// <summary>
    /// Server process that is servicing multiple internal/local/high-bandwidth connections.
    /// </summary>
    InternalServer,

    /// <summary>
    /// Client process that is connecting to internal servers (ie. local high-bandwidth connections).
    /// </summary>
    InternalClient,

    /// <summary>
    /// Server process that is servicing multiple external/relative-low-bandwidth connections.
    /// </summary>
    ExternalServer,

    /// <summary>
    /// Client process that is connection to an external server (ie. a typical end-user client setup, relatively low bandwidth).
    /// </summary>
    ExternalClient,

    /// <summary>
    /// Highly specialized role for talking on a long-fat-network (super-high bandwidth, high-latency, slight packetloss, semi-dedicated pipe).
    /// </summary>
    Lfn
}