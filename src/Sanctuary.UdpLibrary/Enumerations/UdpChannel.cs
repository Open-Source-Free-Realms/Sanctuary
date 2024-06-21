namespace Sanctuary.UdpLibrary.Enumerations;

public enum UdpChannel : byte
{
    /// <summary>
    /// Unreliable/unordered/buffered.
    /// </summary>
    Unreliable,

    /// <summary>
    /// Unreliable/unordered/unbuffered.
    /// </summary>
    UnreliableUnbuffered,

    /// <summary>
    /// Unreliable/ordered/buffered.
    /// </summary>
    Ordered,

    /// <summary>
    /// Unreliable/ordered/unbuffered.
    /// </summary>
    OrderedUnbuffered,

    /// <summary>
    /// Reliable (as per channel config).
    /// </summary>
    Reliable1,
    Reliable2,
    Reliable3,
    Reliable4,

    Count
}