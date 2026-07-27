using System.Numerics;

using Sanctuary.Game.Entities;

public abstract record MovementGoal
{
    public abstract Vector3 GetPosition();

    public abstract bool ClearOnArrival { get; }

    public sealed record FixedPosition(Vector3 Position) : MovementGoal
    {
        public override Vector3 GetPosition() => Position;
        public override bool ClearOnArrival => true;
    }

    public sealed record ChaseEntity(Player Target) : MovementGoal
    {
        public override Vector3 GetPosition() =>
            new(Target.Position.X, Target.Position.Y, Target.Position.Z);
        public override bool ClearOnArrival => false;
    }
}