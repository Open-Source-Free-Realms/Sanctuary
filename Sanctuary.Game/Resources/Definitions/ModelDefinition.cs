namespace Sanctuary.Game.Resources.Definitions;

public class ModelDefinition
{
    public int Id;
    public int RaceId;
    public int Gender;
    public int Age;
    public string ModelFileName = null!;
    public string Description = null!;
    public float Scale;
    public string Descriptor = null!;
    public int MaterialType;
    public float WaterDisplacement;
    public int TeleportEffectId;
    public float CameraDistance;
    public float CameraAngle;
    public float NamePlateOffset;
    public float CapsuleHeight;
    public bool IsValidForPC;
    public float Radius;
}