using System.Collections.Immutable;
using System.Runtime.InteropServices;
using AutoSeller.Core.Driver.Model;

namespace AutoSeller.Core.Travel.Model;

internal readonly record struct ObjectNameRow(uint RowId, string Name);

internal sealed class SummoningBellCatalog
{
    public const uint SummoningBellRow = 2_000_401;
    public const uint SecondSummoningBellRow = 2_000_441;

    private readonly ImmutableArray<uint> baseIds;
    private readonly PromptText name;

    private SummoningBellCatalog(ImmutableArray<uint> baseIds, PromptText name)
    {
        this.baseIds = baseIds;
        this.name = name;
    }

    public static SummoningBellCatalog Known { get; } = new([SummoningBellRow, SecondSummoningBellRow], default);

    public ImmutableArray<uint> BaseIds => baseIds;

    public static SummoningBellCatalog Build(ReadOnlySpan<ObjectNameRow> rows, string bellName)
    {
        var name = PromptText.From(bellName);
        var ids = new List<uint>(Known.baseIds.Length + rows.Length) { SummoningBellRow, SecondSummoningBellRow };
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (name.Matches(row.Name) && !ids.Contains(row.RowId))
            {
                ids.Add(row.RowId);
            }
        }

        var sorted = ids.ToArray();
        Array.Sort(sorted);
        return new SummoningBellCatalog(ImmutableCollectionsMarshal.AsImmutableArray(sorted), name);
    }

    public bool IsBellBaseId(uint baseId)
    {
        return baseIds.AsSpan().BinarySearch(baseId) >= 0;
    }

    public bool IsBellName(ReadOnlySpan<char> shownName)
    {
        return name.Matches(shownName);
    }
}
