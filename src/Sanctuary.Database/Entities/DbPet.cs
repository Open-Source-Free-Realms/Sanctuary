using System;
using System.Collections.Generic;

namespace Sanctuary.Database.Entities;

public class DbPet
{
    public const float MaxNeed = 100f;

    public int Id { get; set; }

    public int Definition { get; set; }
    public int Tint { get; set; }
    public string Name { get; set; } = string.Empty;

    public float Hunger { get; set; } = MaxNeed;
    public float Hygiene { get; set; } = MaxNeed;
    public float Play { get; set; } = MaxNeed;
    public float Mood { get; set; } = MaxNeed;

    public int Trait { get; set; }

    public DateTimeOffset? TreasureFoundAt { get; set; }

    public int EquippedPetTrick1 { get; set; }
    public int EquippedPetTrick2 { get; set; }
    public int EquippedPetTrick3 { get; set; }
    public int EquippedPetTrick4 { get; set; }

    public int ShampooItemId { get; set; }
    public int ConditionerItemId { get; set; }
    public int ToyItemId { get; set; }
    public int BrushItemId { get; set; }
    public int FoodBowlItemId { get; set; }
    public int FoodItemId { get; set; }
    public int VitaminItemId { get; set; }

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<DbItem> Items { get; set; } = new HashSet<DbItem>();

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;
}
