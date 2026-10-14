using System.Collections.Immutable;
using AutoSeller.Core.Stats;
using AutoSeller.Core.Stats.Model;

namespace AutoSeller.Windows.Sections;

internal static class HistoryOrder
{
    private static int[] order = [];
    private static int revision = -1;
    private static int count = -1;
    private static ImmutableArray<RunRecord> sorting = [];

    public static ReadOnlySpan<int> Newest(RunHistory history)
    {
        var records = history.Records;
        if (history.Revision == revision && records.Length == count)
        {
            return order;
        }

        revision = history.Revision;
        count = records.Length;
        if (order.Length != count)
        {
            order = new int[count];
        }

        for (var index = 0; index < count; index++)
        {
            order[index] = index;
        }

        sorting = records;
        Array.Sort(order, static (left, right) =>
        {
            var compared = sorting[right].EndedAtUtc.CompareTo(sorting[left].EndedAtUtc);
            return compared != 0 ? compared : right.CompareTo(left);
        });
        sorting = [];
        return order;
    }
}
