using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Core.AutoRetainer.Model;

internal static class PostprocessWork
{
    public const int MarketToRead = 1;

    public static int Count(SellPlan plan, ulong retainerId)
    {
        var unread = plan.Input.MarketOf(retainerId) is null;
        var rows = plan.Rows;
        var count = unread ? MarketToRead : 0;
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (unread ? row.Kind == RunRowKind.Lot && !row.IsPlaced : row.RetainerId == retainerId && row.Searches)
            {
                count++;
            }
        }

        return count;
    }
}
