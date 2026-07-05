using System;
using System.Net;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sanctuary.Core.Configuration;
using Sanctuary.Core.Cryptography;
using Sanctuary.Core.IO;
using Sanctuary.Login.Handlers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.UdpLibrary;
using Sanctuary.UdpLibrary.Enumerations;

namespace Sanctuary.Login;

public class LoginConnection : UdpConnection
{
    private readonly ILogger _logger;

    private ICipher _cipher;
    private bool _useEncryption = true; // Hardcoded in the client.

    private LoginServerOptions _options;

    public ulong UserId { get; set; }

    public LoginConnection(ILogger<LoginConnection> logger, IOptionsMonitor<LoginServerOptions> options, LoginServer loginServer, SocketAddress socketAddress, int connectCode) : base(loginServer, socketAddress, connectCode)
    {
        _logger = logger;

        _options = options.CurrentValue;
        options.OnChange(o => _options = o);

        _cipher = new CipherRC4();

        // Hardcoded in the client.
        _cipher.Initialize(Convert.FromBase64String("F70IaxuU8C/w7FPXY1ibXw=="));
    }


    public override void OnTerminated()
    {
        var reason = DisconnectReason == DisconnectReason.OtherSideTerminated
            ? OtherSideDisconnectReason
            : DisconnectReason;

        _logger.LogInformation("{connection} disconnected from {ip}. {reason}", this, EndPoint.Address, reason);
    }

    public override void OnRoutePacket(Span<byte> data)
    {
        if ((!_useEncryption || !_cipher.Decrypt(data, out var finalLength))
            && (_useEncryption || !PacketUtils.UnwrapPacket(data, out finalLength, _cipher)))
        {
            _logger.LogError("{connection} failed to unwrap/decrypt packet. ( Data: {data} )", this, Convert.ToHexString(data));
            return;
        }

        OnHandlePacket(data.Slice(0, finalLength));
    }

    private void OnHandlePacket(Span<byte> data)
    {
        var reader = new PacketReader(data);

        if (!reader.TryRead(out byte opCode))
        {
            _logger.LogError("Failed to read opcode from packet. ( Data: {data} )", Convert.ToHexString(data));
            return;
        }

        var handled = opCode switch
        {
            LoginRequest.OpCode => LoginRequestHandler.HandlePacket(this, data),
            CharacterCreateRequest.OpCode => CharacterCreateRequestHandler.HandlePacket(this, data),
            CharacterLoginRequest.OpCode => CharacterLoginRequestHandler.HandlePacket(this, data),
            CharacterDeleteRequest.OpCode => CharacterDeleteRequestHandler.HandlePacket(this, data),
            CharacterSelectInfoRequest.OpCode => CharacterSelectInfoRequestHandler.HandlePacket(this),
            ServerListRequest.OpCode => ServerListRequestHandler.HandlePacket(this),
            TunnelAppPacketClientToServer.OpCode => TunnelAppPacketClientToServerHandler.HandlePacket(this, data),
            _ => false
        };

#if DEBUG
        if (!handled)
        {
            reader.Reset();
            System.Diagnostics.Debug.WriteLine(reader.ReadExternalLoginPacketName());

            _logger.LogWarning("{connection} received an unhandled packet. ( OpCode: {opcode}, Data: {data} )", this, opCode, Convert.ToHexString(data));
        }
#endif
    }

    public override void OnCrcReject(Span<byte> data)
    {
        _logger.LogError("[CrcReject] UserId: {userid}, Data: {data}", UserId, Convert.ToHexString(data));
    }

    public override void OnPacketCorrupt(Span<byte> data, UdpCorruptionReason reason)
    {
        _logger.LogError("[PacketCorrupt] UserId: {userid}, Reason: {reason}, Data: {data}", UserId, reason, Convert.ToHexString(data));
    }

    public void SendTunneled(ISerializablePacket packet, bool reliable = true, bool secure = false)
    {
        var packetTunneledClientPacket = new TunnelAppPacketServerToClient
        {
            Payload = packet.Serialize()
        };

        Send(packetTunneledClientPacket, reliable, secure);
    }

    public void Send(ISerializablePacket packet, bool reliable = true, bool secure = false)
    {
        var data = packet.Serialize();

        if (secure)
            InternalSendSecure(data);
        else
            InternalSend(data, reliable);
    }

    private void InternalSend(Span<byte> data, bool reliable)
    {
        if (_useEncryption)
        {
            InternalSendSecure(data);
            return;
        }

        Send(reliable ? UdpChannel.Reliable1 : UdpChannel.Unreliable, data);
    }

    private void InternalSendSecure(Span<byte> data)
    {
        if (_cipher is null || !_cipher.IsInitialized)
            return;

        using var writer = new PacketWriter();

        if (_useEncryption)
        {
            if (!_cipher.Encrypt(data, writer))
                return;
        }
        else
        {
            if (!PacketUtils.WrapPacket(data, writer, true, _cipher))
                return;
        }

        Send(UdpChannel.Reliable1, writer.Buffer);
    }

    public void ForceDisconnect(int reason)
    {
        var packet = new ForceDisconnect
        {
            Reason = reason
        };

        Send(packet);

        Disconnect();
    }

    #region Packet Compression

    protected override int DecryptUserSupplied(Span<byte> destData, Span<byte> sourceData)
    {
        if (!_options.UseCompression)
            return base.DecryptUserSupplied(destData, sourceData);

        if (sourceData[0] == 1)
        {
            return ZLib.Decompress(sourceData.Slice(1), destData);
        }
        else
        {
            sourceData.Slice(1).CopyTo(destData);

            return sourceData.Length - 1;
        }
    }

    protected override int EncryptUserSupplied(Span<byte> destData, Span<byte> sourceData)
    {
        if (!_options.UseCompression)
            return base.EncryptUserSupplied(destData, sourceData);

        if (sourceData.Length >= 24)
        {
            var compressedLength = ZLib.Compress(sourceData, destData.Slice(1));

            if (compressedLength > 0 && compressedLength < sourceData.Length)
            {
                destData[0] = 1;

                return compressedLength + 1;
            }
        }

        destData[0] = 0;

        sourceData.CopyTo(destData.Slice(1));

        return sourceData.Length + 1;
    }

    #endregion
}
