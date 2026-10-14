using AutoSeller.Core.Planning.Model;

namespace AutoSeller.Core.AutoRetainer.Model;

internal static class PostprocessWork
{
    public const int MarketToRead = 1;

    public static int Count(SellPlan plan, ulong retainerId)
    {
        if (plan.Input.MarketOf(retainerId) is null)
        {
            return MarketToRead;
        }

        var rows = plan.Rows;
        var count = 0;
        for (var index = 0; index < rows.Length; index++)
        {
            if (rows[index].RetainerId == retainerId && rows[index].Searches)
            {
                count++;
            }
        }

        return count;
    }
}
