namespace AutoSeller.Core.Stats;

internal sealed class RunHistory
{
    private readonly List<RunRecord> records = [];

    public IReadOnlyList<RunRecord> Records => records;
}
