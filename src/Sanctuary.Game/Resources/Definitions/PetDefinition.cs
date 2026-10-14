using System.Collections.Generic;

using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Resources.Definitions;

public class PetDefinition
{
    public int Id { get; set; }
    public Dictionary<int, PetTrickDefinition> TrickAnimations { get; set; } = [];
    public List<PetIdleAnimationDefinition> IdleAnimations { get; set; } = [];
    public List<PetIdleAnimationDefinition> BathroomAnimations { get; set; } = [];
    public List<PetRestPostureDefinition> RestPostures { get; set; } = [];
    public int IconId { get; set; }
    public int DefaultTintId { get; set; }
    public string? DefaultTintAlias { get; set; }
    public bool IsVanityPet { get; set; }
    public List<PetTrickInfo> PetTricks { get; set; } = [];
    public List<KeyValuePair<int, ProfileItemClassData>> ItemClasses { get; set; } = [];
}

public class PetIdleAnimationDefinition
{
    public int AnimationId { get; set; }
    public int DurationMs { get; set; }
}

public class PetRestPostureDefinition
{
    public int EnterAnimationId { get; set; }
    public int EnterDurationMs { get; set; }
    public int ExitAnimationId { get; set; }
    public int ExitDurationMs { get; set; }
    public List<int> LoopAnimationIds { get; set; } = [];
    public List<PetIdleAnimationDefinition> FidgetAnimations { get; set; } = [];
}

public class PetTrickDefinition
{
    public int AnimationId { get; set; }
    public int OwnerAnimationId { get; set; }
    public int DurationMs { get; set; }
    public int StartDelayMs { get; set; }
}
