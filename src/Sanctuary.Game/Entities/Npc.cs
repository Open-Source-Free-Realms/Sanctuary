using System.Numerics;

using Sanctuary.Packet;
using Sanctuary.Packet.Common;

using System.Collections.Concurrent;

namespace Sanctuary.Game.Entities;

public class Npc : IEntity, IEntityInteract
{
    public static ulong UniqueGuid = 100_000_000_000u;

    public ulong Guid { get; init; } = UniqueGuid++;
    public Vector4 Position { get; set; }
    public Quaternion Rotation { get; set; }

    public bool Visible { get; set; }

    public required IZone Zone { get; set; }
    public ConcurrentDictionary<ulong, IEntity> VisibleEntities { get; } = new();

    public int NameId;

    public int ModelId;

    public float Scale;

    public int Disposition;

    public string TextureAlias = null!;

    public string TintAlias = null!;
    public int TintId;

    public string Name = null!;

    public int CompositeEffectId;

    public bool HideNamePlate;

    public int ImageSetId;

    public void OnInteract(IEntity other)
    {
        if (other is not Player player)
            return;

        var commandPacketInteractionList = new CommandPacketInteractionList();

        commandPacketInteractionList.List.Guid = Guid;

        var interactions = new[]
        {
            new InteractionData // Remove
            {
                EventId = 999999,
                IconId = 135,
                ButtonText = 18704
            }
        };

        commandPacketInteractionList.List.Interactions.AddRange(interactions);

        player.SendTunneled(commandPacketInteractionList);
    }

    public virtual void OnEntityAdd(IEntity entity)
    {
        VisibleEntities.TryAdd(entity.Guid, entity);
    }

    public virtual void OnEntityRemove(IEntity entity)
    {
        VisibleEntities.TryRemove(entity.Guid, out _);
    }

    public virtual void Update()
    {
    }

    public virtual void UpdateEverySecond()
    {
    }

    public virtual PlayerUpdatePacketAddNpc GetAddNpcPacket()
    {
        var packet = new PlayerUpdatePacketAddNpc
        {
            Guid = Guid,

            NameId = NameId,

            ModelId = ModelId,

            Unknown = default,

            TextureAlias = TextureAlias,
            TintAlias = TintAlias,

            TintId = TintId,

            Scale = Scale,

            Position = Position,
            Rotation = Rotation,

            // playerUpdatePacketAddNpc.Attachments = TODO

            Unknown26 = default,

            Disposition = Disposition,

            AnimSlotId = 1,

            Unknown16 = default,
            VerticalOffset = default,

            CompositeEffectId = CompositeEffectId,

            WieldType = default,

            Name = Name,

            HideNamePlate = HideNamePlate,

            Unknown22 = default,
            Unknown23 = default,
            Unknown24 = default,

            TerrainObjectId = default,

            Speed = default,

            Unknown28 = default,

            InteractRange = 100,

            Unknown30 = default,
            Unknown31 = default,
            Unknown32 = default,

            Unknown33 = default,
            Unknown34 = default,

            SubTextNameId = default,

            Unknown36 = default,
            TemporaryAppearance = default,

            // playerUpdatePacketAddNpc.EffectTags = TODO

            Unknown38 = default,
            Unknown39 = default,
            Unknown40 = default,
            Unknown41 = default,
            Unknown42 = default,

            HasTilt = default,

            // playerUpdatePacketAddNpc.Customization = TODO

            Tilt = default,

            Unknown45 = default,

            AreaDefinitionId = default,

            ImageSetId = default,

            IsInteractable = true,

            RiderGuid = default,

            Unknown50 = default,

            Unknown51 = default,

            Unknown52 = default,

            Unknown53 = default,

            Unknown54 = default,

            Unknown55 = default,

            Unknown56 = default,
            Unknown57 = default,
            Unknown58 = default,

            // playerUpdatePacketAddNpc.Head = TODO
            // playerUpdatePacketAddNpc.Hair = TODO
            // playerUpdatePacketAddNpc.ModelCustomization = TODO

            ReplaceTerrainObject = default,

            Unknown63 = default,
            Unknown64 = default,

            FlyByEffectId = default,

            ActiveProfile = default,

            Unknown67 = default,
            Unknown68 = default,

            NameScale = default,

            NameplateImageId = default
        };

        return packet;
    }

    public virtual void Dispose()
    {
        Zone.RemoveEntity(this);
    }
}