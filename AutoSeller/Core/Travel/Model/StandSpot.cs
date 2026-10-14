using System.Numerics;

namespace AutoSeller.Core.Travel.Model;

internal static class StandSpot
{
    private static readonly Vector2 stepOffFromOverTheTarget = Vector2.UnitY;

    public static Vector3 Beside(Vector3 target, Vector3 from, float standOffMeters)
    {
        var toward = new Vector2(from.X - target.X, from.Z - target.Z);
        var direction = toward.LengthSquared() > 0f ? Vector2.Normalize(toward) : stepOffFromOverTheTarget;
        return new Vector3(target.X + direction.X * standOffMeters, from.Y, target.Z + direction.Y * standOffMeters);
    }
}
