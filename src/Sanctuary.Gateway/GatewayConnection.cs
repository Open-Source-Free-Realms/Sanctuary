using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sanctuary.Core.Configuration;
using Sanctuary.Core.IO;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game;
using Sanctuary.Game.Entities;
using Sanctuary.Gateway.Handlers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.UdpLibrary;
using Sanctuary.UdpLibrary.Enumerations;

namespace Sanctuary.Gateway;

public class GatewayConnection : UdpConnection
{
    private readonly ILogger _logger;
    private readonly LoginClient _loginClient;
    private readonly IZoneManager _zoneManager;
    private readonly GatewayServer _gatewayServer;
    private readonly GatewayServerOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly IResourceManager _resourceManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    // Player will only be null during login.
    public Player Player { get; private set; } = null!;

    public string Locale { get; set; } = "en_US";

    public GatewayConnection(ILogger<GatewayConnection> logger, IOptions<GatewayServerOptions> options, IZoneManager zoneManager, LoginClient loginClient, GatewayServer gatewayServer, IResourceManager resourceManager, IServiceProvider serviceProvider, IDbContextFactory<DatabaseContext> dbContextFactory, SocketAddress socketAddress, int connectCode) : base(gatewayServer, socketAddress, connectCode)
    {
        _logger = logger;
        _options = options.Value;
        _loginClient = loginClient;
        _zoneManager = zoneManager;
        _gatewayServer = gatewayServer;
        _resourceManager = resourceManager;
        _serviceProvider = serviceProvider;
        _dbContextFactory = dbContextFactory;
    }

    public override void OnTerminated()
    {
        var reason = DisconnectReason == DisconnectReason.OtherSideTerminated
            ? OtherSideDisconnectReason
            : DisconnectReason;

        _logger.LogInformation("{connection} disconnected. {reason}", this, reason);

        _loginClient.SendCharacterLogout(Player.Guid);

        // TODO: Save player info to database.

        // Just in case check if player is null.
        Player?.Dispose();
    }

    public override void OnRoutePacket(Span<byte> data)
    {
        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode))
            return;

        var handled = opCode switch
        {
            PacketLogin.OpCode => PacketLoginHandler.HandlePacket(this, data),
            PacketTunneledClientPacket.OpCode => PacketTunneledClientPacketHandler.HandlePacket(this, data),
            PacketTunneledClientWorldPacket.OpCode => PacketTunneledClientWorldPacketHandler.HandlePacket(this, data),
            _ => false
        };

        if (!handled)
        {
            _logger.LogWarning("{connection} received an unhandled packet. ( OpCode: {opcode}, Data: {data} )", this, opCode, Convert.ToHexString(data));
        }
    }

    public override void OnCrcReject(Span<byte> data)
    {
        _logger.LogError("[CrcReject] Guid: {guid}, Data: {data}", Player?.Guid, Convert.ToHexString(data));
    }

    public override void OnPacketCorrupt(Span<byte> data, UdpCorruptionReason reason)
    {
        _logger.LogError("[PacketCorrupt] Guid: {guid}, Reason: {reason}, Data: {data}", Player?.Guid, reason, Convert.ToHexString(data));
    }

    public void Send(ISerializablePacket packet)
    {
        var data = packet.Serialize();

        Send(UdpChannel.Reliable1, data);
    }

    public void SendTunneled(ISerializablePacket packet)
    {
        var packetTunneled = new PacketTunneledClientPacket
        {
            Payload = packet.Serialize()
        };

        Send(packetTunneled);
    }

    [Obsolete]
    public void SendTunneled(byte[] data)
    {
        var packetTunneledClientPacket = new PacketTunneledClientPacket
        {
            Payload = data
        };

        Send(packetTunneledClientPacket);
    }

    public bool CreatePlayerFromDatabase(DbCharacter dbCharacter)
    {
        var startingZone = _zoneManager.StartingZone;

        if (!startingZone.TryCreatePlayer(dbCharacter.Guid, this, out var player))
        {
            _logger.LogError("Failed to create player entity.");
            return false;
        }

        Player = player;

        // Start - ClientPcData

        Player.Model = dbCharacter.Model;

        Player.Head = dbCharacter.Head;
        Player.Hair = dbCharacter.Hair;

        Player.HairColor = dbCharacter.HairColor;
        Player.EyeColor = dbCharacter.EyeColor;

        Player.SkinTone = dbCharacter.SkinTone;

        Player.FacePaint = dbCharacter.FacePaint;
        Player.ModelCustomization = dbCharacter.ModelCustomization;

        Player.Position = dbCharacter.Position;
        Player.Rotation = dbCharacter.Rotation;

        Player.Name.FirstName = dbCharacter.FirstName;
        Player.Name.LastName = dbCharacter.LastName;

        Player.Birthday = dbCharacter.Created;

        Player.MembershipStatus = dbCharacter.MembershipStatus;
        Player.ShowMemberNagScreen = _options.ShowMemberNagScreen;

        foreach (var dbProfile in dbCharacter.Profiles)
        {
            if (!_resourceManager.Profiles.TryGetValue(dbProfile.Id, out var profileData))
                continue;

            var clientPcProfile = new ClientPcProfile();

            clientPcProfile.Id = dbProfile.Id;

            clientPcProfile.NameId = profileData.NameId;
            clientPcProfile.DescriptionId = profileData.DescriptionId;

            clientPcProfile.Icon = profileData.Icon;

            foreach (var dbItem in dbProfile.Items)
            {
                if (!_resourceManager.ClientItemDefinitions.TryGetValue(dbItem.Definition, out var clientItemDefinition))
                    continue;

                if (!clientPcProfile.ItemClassData.ContainsKey(clientItemDefinition.Class))
                {
                    var profileItemClassData = new ProfileItemClassData
                    {
                        Id = clientItemDefinition.Class
                    };

                    clientPcProfile.ItemClassData.Add(clientItemDefinition.Class, profileItemClassData);
                }

                if (clientPcProfile.Items.TryGetValue(clientItemDefinition.Slot, out var profileItem))
                    profileItem.Id = dbItem.Id;
                else
                {
                    profileItem = new ProfileItem
                    {
                        Id = dbItem.Id,
                        Slot = clientItemDefinition.Slot
                    };

                    clientPcProfile.Items.Add(clientItemDefinition.Slot, profileItem);
                }
            }

            clientPcProfile.BadgeImageSet = profileData.BadgeImageSet;
            clientPcProfile.ButtonImageSet = profileData.ButtonImageSet;
            clientPcProfile.Rank = dbProfile.Level;
            clientPcProfile.RankPercent = dbProfile.LevelXP;

            Player.Profiles.Add(clientPcProfile);

            if (!Player.ProfileTypes.Any(x => x.Type == profileData.Type))
            {
                Player.ProfileTypes.Add(new ProfileTypeEntry
                {
                    Type = profileData.Type,
                    ProfileId = profileData.Id
                });
            }
        }

        Player.ActiveProfile = dbCharacter.ActiveProfileId;

        foreach (var dbItem in dbCharacter.Items)
        {
            Player.Items.Add(new ClientItem
            {
                Id = dbItem.Id,
                Tint = dbItem.Tint,
                Count = dbItem.Count,
                Definition = dbItem.Definition
            });
        }

        Player.Gender = dbCharacter.Gender;

        foreach (var dbMount in dbCharacter.Mounts)
        {
            if (!_resourceManager.Mounts.TryGetValue(dbMount.Id, out var mountDefinition))
                continue;

            Player.Mounts.Add(new PacketMountInfo
            {
                Id = mountDefinition.Id,
                NameId = mountDefinition.NameId,
                ImageSetId = mountDefinition.ImageSetId,
                TintId = mountDefinition.TintId,
                TintAlias = mountDefinition.TintAlias,
                MembersOnly = mountDefinition.MembersOnly,
                IsUpgradable = mountDefinition.IsUpgradable,
                IsUpgraded = dbMount.IsUpgraded,
            });
        }

        // TODO

        // Start - Store on DB
        var clientActionBar = new ClientActionBar();

        clientActionBar.Id = 2; // ItemActionBar

        clientActionBar.Slots.Add(0, new ActionBarSlot());
        clientActionBar.Slots.Add(1, new ActionBarSlot());
        clientActionBar.Slots.Add(2, new ActionBarSlot());
        clientActionBar.Slots.Add(3, new ActionBarSlot());

        Player.ActionBars.Add(clientActionBar.Id, clientActionBar);
        // End - Store on DB

        foreach (var dbTitle in dbCharacter.Titles)
        {
            if (!_resourceManager.PlayerTitles.TryGetValue(dbTitle.Id, out var playerTitle))
                continue;

            Player.Titles.Add(playerTitle);
        }

        Player.ActiveTitle = dbCharacter.ActiveTitleId ?? 0;

        Player.VipRank = dbCharacter.VipRank;

        // End ClientPcData

        Player.ChatBubbleForegroundColor = dbCharacter.ChatBubbleForegroundColor;
        Player.ChatBubbleBackgroundColor = dbCharacter.ChatBubbleBackgroundColor;
        Player.ChatBubbleSize = dbCharacter.ChatBubbleSize;

        return true;
    }

    private bool SavePlayerToDatabase()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbCharacter = dbContext.Characters
            .Include(x => x.Items)
            .Include(x => x.Titles)
            .Include(x => x.Mounts)
            .Include(x => x.Profiles)
                .ThenInclude(x => x.Items)
            .AsSplitQuery()
            .SingleOrDefault(x => x.Guid == Player.Guid);

        if (dbCharacter is null)
        {
            _logger.LogError("Failed to get character data from database.");
            return false;
        }

        // TODO

        return true;
    }

    public void SendInitializationParameters()
    {
        var packetInitializationParameters = new PacketInitializationParameters();

        packetInitializationParameters.Environment = _options.Environment;

        SendTunneled(packetInitializationParameters);
    }

    public void SendZoneDetails()
    {
        var packetSendZoneDetails = new PacketSendZoneDetails
        {
            Name = Player.Zone.Definition.Name,
            Id = Player.Zone.Definition.Id
        };

        SendTunneled(packetSendZoneDetails);
    }

    public void ClientGameSettings()
    {
        var packetClientGameSettings = new PacketClientGameSettings
        {
            Unknown = 4,
            Unknown2 = 7,
            PowerHourEffectTag = 268,
            Unknown4 = true,
            GameTimeScalar = 1.0f
        };

        SendTunneled(packetClientGameSettings);
    }

    public void SendItemDefinitions()
    {
        var clientItemDefinitions = new List<ClientItemDefinition>();

        foreach (var item in Player.Items)
        {
            if (!_resourceManager.ClientItemDefinitions.TryGetValue(item.Definition, out var clientItemDefinition))
                continue;

            clientItemDefinitions.Add(clientItemDefinition);
        }

        using var writer = new PacketWriter();

        writer.Write(clientItemDefinitions);

        var playerUpdatePacketItemDefinitions = new PlayerUpdatePacketItemDefinitions();

        playerUpdatePacketItemDefinitions.Payload = writer.Buffer;

        SendTunneled(playerUpdatePacketItemDefinitions);
    }

    public void SendSelfToClient()
    {
        var packetSendSelfToClient = new PacketSendSelfToClient();

        packetSendSelfToClient.Payload = Player.Serialize();

        SendTunneled(packetSendSelfToClient);
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