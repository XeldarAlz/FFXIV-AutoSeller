using System.Numerics;

namespace AutoSeller.Core.Travel.Model;

public enum BellChoice : byte
{
    Nearest,
    InnRoom,
    Picked,
}

public sealed class PickedBell
{
    public uint TerritoryId { get; set; }

    public Vector3 Bell { get; set; }

    public Vector3 Stand { get; set; }
}
