namespace Sanctuary.UdpLibrary.Enumerations;


/// <summary>
/// Encryption methods are allowed to change the packet-size, so raw packet level compression
/// would actually be implemented as a new encryption-method if needed
/// the user-supplied method requires that the both ends of the connection have the user-supplied
/// encrypt handler functions setup to correspond to each other.
/// </summary>
public enum EncryptMethod : byte
{
    None,

    /// <summary>
    /// Use the <see cref="UdpConnection{TConnection}.EncryptUserSupplied"/> function.
    /// </summary>
    UserSupplied,

    /// <summary>
    /// Use the <see cref="UdpConnection{TConnection}.EncryptUserSupplied2"/> function.
    /// </summary>
    UserSupplied2,

    /// <summary>
    /// Slower xor method, but slightly more encrypted.
    /// </summary>
    XorBuffer,

    /// <summary>
    /// Faster using less memory, slightly less well encrypted, use this one as first choice typically.
    /// </summary>
    Xor
}