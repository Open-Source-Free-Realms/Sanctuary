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
    private readonly LoginServer _loginServer;

    private RC4? _recvRC4, _sendRC4;
    private LoginServerOptions _options;

    public ulong Guid { get; set; }

    public LoginConnection(ILogger<LoginConnection> logger, IOptionsMonitor<LoginServerOptions> options, LoginServer loginServer, SocketAddress socketAddress, int connectCode) : base(loginServer, socketAddress, connectCode)
    {
        _logger = logger;
        _loginServer = loginServer;

        _options = options.CurrentValue;
        options.OnChange(o => _options = o);

        if (!string.IsNullOrEmpty(_options.CryptKey))
        {
            _recvRC4 = new RC4(_options.CryptKey);
            _sendRC4 = new RC4(_options.CryptKey);
        }
    }

    public override void OnTerminated()
    {
        var reason = DisconnectReason == DisconnectReason.OtherSideTerminated
            ? OtherSideDisconnectReason
            : DisconnectReason;

        _logger.LogInformation("{connection} disconnected. {reason}", this, reason);
    }

    public override void OnRoutePacket(Span<byte> data)
    {
        _recvRC4?.Apply(data);

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

        if (!handled)
        {
#if DEBUG
            reader.Reset();
            System.Diagnostics.Debug.WriteLine(reader.ReadExternalLoginPacketName());
#endif

            _logger.LogWarning("{connection} received an unhandled packet. ( OpCode: {opcode}, Data: {data} )", this, opCode, Convert.ToHexString(data));
        }
    }

    public override void OnCrcReject(Span<byte> data)
    {
        _logger.LogError("[CrcReject] Guid: {guid}, Data: {data}", Guid, Convert.ToHexString(data));
    }

    public override void OnPacketCorrupt(Span<byte> data, UdpCorruptionReason reason)
    {
        _logger.LogError("[PacketCorrupt] Guid: {guid}, Reason: {reason}, Data: {data}", Guid, reason, Convert.ToHexString(data));
    }

    public void Send(ISerializablePacket packet)
    {
        var data = packet.Serialize();

        _sendRC4?.Apply(data);

        Send(UdpChannel.Reliable1, data);
    }

    public void SendTunneled(ISerializablePacket packet)
    {
        var packetTunneledClientPacket = new TunnelAppPacketServerToClient
        {
            Payload = packet.Serialize()
        };

        Send(packetTunneledClientPacket);
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