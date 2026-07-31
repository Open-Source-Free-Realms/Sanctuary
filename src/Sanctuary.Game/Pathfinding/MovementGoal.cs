using System.Numerics;


// TODO: implement a `ChaseEntity`.
// - Alko
public abstract record MovementGoal
{
    public abstract Vector3 GetPosition();

    public abstract bool ClearOnArrival { get; }

    public sealed record FixedPosition(Vector3 Position) : MovementGoal
    {
        public override Vector3 GetPosition() => Position;
        public override bool ClearOnArrival => true;
    }
}