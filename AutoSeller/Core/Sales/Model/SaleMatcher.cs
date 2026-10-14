using System.Collections.Immutable;

namespace AutoSeller.Core.Sales.Model;

internal static class SaleMatcher
{
    public static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan RepeatedNoticeWindow = TimeSpan.FromSeconds(5);

    private const int NotFound = -1;

    public static ImmutableArray<SaleRecord> Merge(ReadOnlySpan<SaleRecord> records)
    {
        if (records.IsEmpty)
        {
            return ImmutableArray<SaleRecord>.Empty;
        }

        var ordered = records.ToArray();
        Array.Sort(ordered, CompareForGrouping);
        var merged = new List<SaleRecord>(ordered.Length);
        var groupStart = 0;
        for (var index = 1; index <= ordered.Length; index++)
        {
            if (index < ordered.Length && IsSameLotSale(ordered[groupStart], ordered[index]))
            {
                continue;
            }

            MergeGroup(ordered.AsSpan(groupStart, index - groupStart), merged);
            groupStart = index;
        }

        merged.Sort(CompareByTime);
        return [.. merged];
    }

    private static void MergeGroup(ReadOnlySpan<SaleRecord> group, List<SaleRecord> merged)
    {
        var histories = Distinct(Slice(group, SaleSource.History), IsSameHistoryEntry);
        var notices = Distinct(Slice(group, SaleSource.Notice), IsRepeatedNotice);
        var diffs = Slice(group, SaleSource.Diff).ToArray();
        var confirmed = Absorb(histories, notices);
        confirmed.Sort(CompareByTime);
        merged.AddRange(Absorb(confirmed, diffs));
    }

    private static ReadOnlySpan<SaleRecord> Slice(ReadOnlySpan<SaleRecord> group, SaleSource source)
    {
        var start = 0;
        while (start < group.Length && group[start].Source != source)
        {
            start++;
        }

        var end = start;
        while (end < group.Length && group[end].Source == source)
        {
            end++;
        }

        return group[start..end];
    }

    private static List<SaleRecord> Distinct(ReadOnlySpan<SaleRecord> records, Func<SaleRecord, SaleRecord, bool> isDuplicate)
    {
        var kept = new List<SaleRecord>(records.Length);
        for (var index = 0; index < records.Length; index++)
        {
            var record = records[index];
            var duplicate = IndexOfDuplicate(kept, record, isDuplicate);
            if (duplicate == NotFound)
            {
                kept.Add(record);
                continue;
            }

            kept[duplicate] = FillGaps(kept[duplicate], record);
        }

        return kept;
    }

    private static int IndexOfDuplicate(List<SaleRecord> kept, in SaleRecord record, Func<SaleRecord, SaleRecord, bool> isDuplicate)
    {
        for (var index = 0; index < kept.Count; index++)
        {
            if (isDuplicate(kept[index], record))
            {
                return index;
            }
        }

        return NotFound;
    }

    private static SaleRecord FillGaps(in SaleRecord kept, in SaleRecord duplicate)
    {
        return kept with
        {
            ListedAtUtc = kept.HasListingTime ? kept.ListedAtUtc : duplicate.ListedAtUtc,
            UnitPrice = kept.HasUnitPrice ? kept.UnitPrice : duplicate.UnitPrice,
        };
    }

    private static List<SaleRecord> Absorb(IReadOnlyList<SaleRecord> primaries, IReadOnlyList<SaleRecord> secondaries)
    {
        var result = new List<SaleRecord>(primaries.Count + secondaries.Count);
        var taken = new bool[secondaries.Count];
        for (var primaryIndex = 0; primaryIndex < primaries.Count; primaryIndex++)
        {
            var primary = primaries[primaryIndex];
            var partner = FindPartner(primary, secondaries, taken);
            if (partner == NotFound)
            {
                result.Add(primary);
                continue;
            }

            taken[partner] = true;
            result.Add(Combine(primary, secondaries[partner]));
        }

        for (var secondaryIndex = 0; secondaryIndex < secondaries.Count; secondaryIndex++)
        {
            if (!taken[secondaryIndex])
            {
                result.Add(secondaries[secondaryIndex]);
            }
        }

        return result;
    }

    private static int FindPartner(in SaleRecord primary, IReadOnlyList<SaleRecord> secondaries, bool[] taken)
    {
        var best = NotFound;
        var bestDistance = TimeSpan.MaxValue;
        for (var index = 0; index < secondaries.Count; index++)
        {
            var candidate = secondaries[index];
            if (taken[index]
                || !AreCompatibleRetainers(primary, candidate)
                || !PricesAgree(primary, candidate)
                || !CouldHaveSoldAt(candidate, primary.SoldAtUtc))
            {
                continue;
            }

            var distance = (candidate.SoldAtUtc - primary.SoldAtUtc).Duration();
            if (distance < bestDistance)
            {
                best = index;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static SaleRecord Combine(in SaleRecord primary, in SaleRecord secondary)
    {
        var soldAtUtc = primary.SoldAtUtc;
        var soldAfterUtc = primary.SoldAfterUtc;
        if (primary.Source != SaleSource.History)
        {
            soldAtUtc = Earlier(primary.SoldAtUtc, secondary.SoldAtUtc);
            soldAfterUtc = Earlier(Later(primary.SoldAfterUtc, secondary.SoldAfterUtc), soldAtUtc);
        }

        return primary with
        {
            SoldAtUtc = soldAtUtc,
            SoldAfterUtc = soldAfterUtc,
            ListedAtUtc = primary.HasListingTime ? primary.ListedAtUtc : secondary.ListedAtUtc,
            UnitPrice = primary.HasUnitPrice ? primary.UnitPrice : secondary.UnitPrice,
            RetainerId = primary.RetainerId != SaleRecord.UnknownRetainer ? primary.RetainerId : secondary.RetainerId,
            NetGil = secondary.Source == SaleSource.Notice ? secondary.NetGil : primary.NetGil,
        };
    }

    private static bool PricesAgree(in SaleRecord first, in SaleRecord second)
    {
        if (first.HasUnitPrice && second.HasUnitPrice)
        {
            return first.UnitPrice == second.UnitPrice;
        }

        var priced = first.HasUnitPrice ? first : second;
        var unpriced = first.HasUnitPrice ? second : first;
        if (!priced.HasUnitPrice || priced.Source == SaleSource.Notice)
        {
            return priced.NetGil == unpriced.NetGil;
        }

        return SalePrice.CouldNetTo(priced.GrossGil, unpriced.NetGil);
    }

    private static bool CouldHaveSoldAt(in SaleRecord record, DateTime atUtc)
    {
        var toleranceTicks = ClockTolerance.Ticks;
        return record.SoldAfterUtc.Ticks - toleranceTicks <= atUtc.Ticks
            && atUtc.Ticks <= record.SoldAtUtc.Ticks + toleranceTicks;
    }

    private static bool AreCompatibleRetainers(in SaleRecord first, in SaleRecord second)
    {
        return first.RetainerId == second.RetainerId
            || first.RetainerId == SaleRecord.UnknownRetainer
            || second.RetainerId == SaleRecord.UnknownRetainer;
    }

    private static bool IsSameHistoryEntry(SaleRecord kept, SaleRecord candidate)
    {
        return kept.RetainerId == candidate.RetainerId
            && kept.SoldAtUtc == candidate.SoldAtUtc
            && PricesAgree(kept, candidate);
    }

    private static bool IsRepeatedNotice(SaleRecord kept, SaleRecord candidate)
    {
        return kept.RetainerId == candidate.RetainerId
            && kept.UnitPrice == candidate.UnitPrice
            && kept.NetGil == candidate.NetGil
            && (candidate.SoldAtUtc - kept.SoldAtUtc).Duration() <= RepeatedNoticeWindow;
    }

    private static bool IsSameLotSale(in SaleRecord first, in SaleRecord second)
    {
        return first.CharacterContentId == second.CharacterContentId
            && first.ItemId == second.ItemId
            && first.IsHq == second.IsHq
            && first.Quantity == second.Quantity;
    }

    private static int CompareForGrouping(SaleRecord first, SaleRecord second)
    {
        var order = first.CharacterContentId.CompareTo(second.CharacterContentId);
        if (order == 0)
        {
            order = first.ItemId.CompareTo(second.ItemId);
        }

        if (order == 0)
        {
            order = first.IsHq.CompareTo(second.IsHq);
        }

        if (order == 0)
        {
            order = first.Quantity.CompareTo(second.Quantity);
        }

        if (order == 0)
        {
            order = ExactnessRank(first.Source).CompareTo(ExactnessRank(second.Source));
        }

        if (order == 0)
        {
            order = first.SoldAtUtc.CompareTo(second.SoldAtUtc);
        }

        if (order == 0)
        {
            order = first.UnitPrice.CompareTo(second.UnitPrice);
        }

        return order != 0 ? order : first.RetainerId.CompareTo(second.RetainerId);
    }

    private static int CompareByTime(SaleRecord first, SaleRecord second)
    {
        var order = first.SoldAtUtc.CompareTo(second.SoldAtUtc);
        return order != 0 ? order : CompareForGrouping(first, second);
    }

    private static int ExactnessRank(SaleSource source)
    {
        return source switch
        {
            SaleSource.History => 0,
            SaleSource.Notice => 1,
            _ => 2,
        };
    }

    private static DateTime Earlier(DateTime first, DateTime second)
    {
        return first <= second ? first : second;
    }

    private static DateTime Later(DateTime first, DateTime second)
    {
        return first >= second ? first : second;
    }
}
