namespace AutoSeller.Core.Retainers.Model;

internal sealed class SnapshotDiffBases
{
    private readonly Dictionary<ulong, RetainerSnapshot> counted = [];

    public RetainerSnapshot? Advance(RetainerSnapshot current, RetainerSnapshot? stored, bool countMatches)
    {
        var retainerId = current.RetainerId;
        var basis = counted.TryGetValue(retainerId, out var held) ? held : stored;
        if (countMatches)
        {
            counted[retainerId] = current;
        }
        else if (basis is not null)
        {
            counted[retainerId] = basis;
        }

        return basis;
    }
}
