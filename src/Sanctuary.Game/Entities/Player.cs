using System;
using System.Linq;
using System.Numerics;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Sanctuary.Packet;
using Sanctuary.Core.IO;
using Sanctuary.UdpLibrary;
using Sanctuary.Packet.Common;
using Sanctuary.Core.Extensions;
using Sanctuary.UdpLibrary.Enumerations;
using System.Diagnostics;

namespace Sanctuary.Game.Entities;

public sealed class Player : ClientPcData, IEntity, IEntitySession, IEntityInteract
{
    private readonly UdpConnection _connection;
    private readonly IResourceManager _resourceManager;

    public bool Visible { get; set; }

    public required IZone Zone { get; set; }
    public ConcurrentDictionary<ulong, IEntity> VisibleEntities { get; } = new();

    private int ZoneAreaId { get; set; }

    public int ChatBubbleForegroundColor { get; set; }
    public int ChatBubbleBackgroundColor { get; set; }
    public int ChatBubbleSize { get; set; }

    public ClientPcProfile? Profile => Profiles.SingleOrDefault(x => x.Id == ActiveProfile);

    public Mount? Mount { get; set; }

    public Player(ulong guid, UdpConnection connection, IResourceManager resourceManager)
    {
        Guid = guid;

        _connection = connection;
        _resourceManager = resourceManager;
    }

    public void Send(ISerializablePacket packet)
    {
        var data = packet.Serialize();

        _connection.Send(UdpChannel.Reliable1, data);
    }

    public void SendToVisible(ISerializablePacket packet, bool sendToSelf = false)
    {
        var visibleEntities = VisibleEntities.ToFrozenDictionary();

        foreach (var visibleEntity in visibleEntities)
            if (visibleEntity.Value is IEntitySession entitySession)
                entitySession.Send(packet);

        if (sendToSelf)
            Send(packet);
    }

    public void SendTunneled(ISerializablePacket packet)
    {
        var packetTunneled = new PacketTunneledClientPacket
        {
            Payload = packet.Serialize()
        };
        
        Send(packetTunneled);
    }

    public void SendTunneledToVisible(ISerializablePacket packet, bool sendToSelf = false)
    {
        var visibleEntities = VisibleEntities.ToFrozenDictionary();

        foreach (var visibleEntity in visibleEntities)
            if (visibleEntity.Value is IEntitySession entitySession)
                entitySession.SendTunneled(packet);

        if (sendToSelf)
            SendTunneled(packet);
    }

    public void Update()
    {
        // UpdateVisibleEntities();
    }

    public void UpdateEverySecond()
    {
        UpdateZoneArea();
    }

    public void OnEntityAdd(IEntity entity)
    {
        if (entity is Player player)
        {
            var playerUpdatePacketAddPc = player.GetAddPcPacket();

            SendTunneled(playerUpdatePacketAddPc);
        }
        else if (entity is Npc npc)
        {
            var playerUpdatePacketAddNpc = npc.GetAddNpcPacket();

            SendTunneled(playerUpdatePacketAddNpc);
        }

        VisibleEntities.TryAdd(entity.Guid, entity);
    }

    public void OnEntityRemove(IEntity entity)
    {
        var playerUpdatePacketRemovePlayerGracefully = new PlayerUpdatePacketRemovePlayerGracefully();

        playerUpdatePacketRemovePlayerGracefully.Guid = entity.Guid;

        playerUpdatePacketRemovePlayerGracefully.Animate = false;
        playerUpdatePacketRemovePlayerGracefully.Delay = 0;
        playerUpdatePacketRemovePlayerGracefully.EffectDelay = 0;
        playerUpdatePacketRemovePlayerGracefully.CompositeEffectId = 46;
        playerUpdatePacketRemovePlayerGracefully.Duration = 1000;

        SendTunneled(playerUpdatePacketRemovePlayerGracefully);

        /* var playerUpdatePacketRemovePlayer = new PlayerUpdatePacketRemovePlayer();

        playerUpdatePacketRemovePlayer.Guid = entity.Guid;

        SendTunneled(playerUpdatePacketRemovePlayer); */

        VisibleEntities.TryRemove(entity.Guid, out _);
    }

    public void OnInteract(IEntity other)
    {
        if (other is not Player player)
            return;

        var commandPacketInteractionList = new CommandPacketInteractionList();

        commandPacketInteractionList.List.Guid = Guid;

        var eventId = 0;

        var interactions = new[]
        {
            new InteractionData // Add Friend
            {
                EventId = eventId++,
                IconId = 134,
                ButtonText = 3370
            },
            /* new InteractionData // Remove Friend
            {
                EventId = eventId++,
                IconId = 135,
                ButtonText = 3371
            },
            new InteractionData // Invite to Group
            {
                EventId = eventId++,
                IconId = 136,
                ButtonText = 3372
            },
            new InteractionData // Remove from Group
            {
                EventId = eventId++,
                IconId = 137,
                ButtonText = 430592
            }, */
            new InteractionData // Trade
            {
                EventId = eventId++,
                IconId = 737,
                ButtonText = 3299
            },
            new InteractionData // Inspect
            {
                EventId = eventId++,
                IconId = 133,
                ButtonText = 3902
            },
            new InteractionData // Ignore
            {
                EventId = eventId++,
                IconId = 9557,
                ButtonText = 2816
            },
            /* new InteractionData // Stop Ignoring
            {
                EventId = eventId++,
                IconId = 12052,
                ButtonText = 2817
            }, */
            new InteractionData // Duel
            {
                EventId = eventId++,
                IconId = 1348,
                ButtonText = 3318
            }
        };

        commandPacketInteractionList.List.Interactions.AddRange(interactions);

        player.SendTunneled(commandPacketInteractionList);
    }

    public void UpdateCharacterStats(params CharacterStat[] characterStats)
    {
        var clientUpdatePacketUpdateStat = new ClientUpdatePacketUpdateStat
        {
            Guid = Guid
        };

        clientUpdatePacketUpdateStat.Stats.AddRange(characterStats);

        SendTunneled(clientUpdatePacketUpdateStat);

        foreach (var characterStat in characterStats)
        {
            Stats[characterStat.Id] = characterStat;

            if (characterStat.Id == CharacterStatId.MaxMovementSpeed)
            {
                var playerUpdatePacketExpectedSpeed = new PlayerUpdatePacketExpectedSpeed
                {
                    Guid = Guid,
                    ExpectedSpeed = characterStat.Float
                };

                SendTunneledToVisible(playerUpdatePacketExpectedSpeed);
            }
        }
    }

    public List<CharacterAttachmentData> GetAttachments()
    {
        var list = new List<CharacterAttachmentData>();

        var activeProfile = Profile;

        if (activeProfile is null)
            return list;

        foreach (var profileItem in activeProfile.Items)
        {
            var clientItem = Items.SingleOrDefault(x => x.Id == profileItem.Value.Id);

            if (clientItem is null)
                continue;

            if (!_resourceManager.ItemDefinitions.TryGetValue(clientItem.Definition, out var itemDefinition))
                continue;

            list.Add(new CharacterAttachmentData
            {
                ModelName = itemDefinition.ModelName,
                TextureAlias = itemDefinition.TextureAlias,
                TintAlias = itemDefinition.TintAlias,
                TintId = clientItem.Tint,
                CompositeEffectId = itemDefinition.CompositeEffectId,
                Slot = itemDefinition.Slot
            });
        }

        return list;
    }

    public PlayerUpdatePacketAddPc GetAddPcPacket()
    {
        var packet = new PlayerUpdatePacketAddPc
        {
            Guid = Guid,

            Name = Name,

            Model = Model,

            ChatBubbleForegroundColor = ChatBubbleForegroundColor,
            ChatBubbleBackgroundColor = ChatBubbleBackgroundColor,
            ChatBubbleSize = ChatBubbleSize,

            Position = Position,
            Rotation = Rotation,

            Attachments = GetAttachments(),

            Head = Head,
            Hair = Hair,

            HairColor = HairColor,
            EyeColor = EyeColor,

            SkinTone = SkinTone,

            FacePaint = FacePaint,
            ModelCustomization = ModelCustomization,

            MaxMovementSpeed = Stats[CharacterStatId.MaxMovementSpeed],

            IsUnderage = Age < 18,
            IsMember = MembershipStatus != 0,

            // playerUpdatePacketAddPc.TemporaryAppearance = 277;

            ActiveProfile = ActiveProfile
        };

        var activeTitle = Titles.FirstOrDefault(x => x.Id == ActiveTitle);

        if (activeTitle is not null)
            packet.Title = activeTitle;

        if (Mount is not null)
        {
            packet.MountGuid = Mount.Guid;

            packet.NameVerticalOffset = Mount.Definition.NameVerticalOffset;
        }

        return packet;
    }

    private void UpdateZoneArea()
    {
        foreach (var areaDefinition in Zone.Definition.AreaDefinitions)
        {
            if (areaDefinition.Shape == "Circle")
            {
                var circle = new Vector3(areaDefinition.X1, 0, areaDefinition.Z1);

                if (Position.IsInCircle(circle, areaDefinition.Radius))
                {
                    SetZoneAreaId(areaDefinition.Id);

                    return;
                }
            }
            else if (areaDefinition.Shape == "Rectangle")
            {
                var p1 = new Vector3(areaDefinition.X1, 0, areaDefinition.Z1);
                var p2 = new Vector3(areaDefinition.X2, 0, areaDefinition.Z2);

                if (Position.IsInRectangle(p1, p2))
                {
                    SetZoneAreaId(areaDefinition.Id);

                    return;
                }
            }
            else
            {
                throw new NotImplementedException(nameof(areaDefinition.Shape));
            }
        }
    }

    private void SetZoneAreaId(int id)
    {
        if (ZoneAreaId == id)
            return;

        ZoneAreaId = id;

        var packetPOIChangeMessage = new PacketPOIChangeMessage
        {
            ZoneId = id
        };

        SendTunneled(packetPOIChangeMessage);
    }

    public void Dispose()
    {
        if (Mount is not null)
            Zone.RemoveEntity(Mount);

        Zone.RemoveEntity(this);
    }
}