using System.Collections.Immutable;
using AutoSeller.Core.Characters.Model;
using AutoSeller.Core.Listings.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using static AutoSeller.Tests.EndToEnd.RunWorld;
using static AutoSeller.Tests.Planning.PlanFixtures;

namespace AutoSeller.Tests.Listings;

internal static class ListingFixtures
{
    public const ulong Alt = Character + 1;
    public const ulong AltRetainer = Beta + 100;
    public const string MainName = "Wren Ashford";
    public const string AltName = "Ilsa Marrow";

    public static CharacterRecord MainCharacter(params RetainerSnapshot[] snapshots)
    {
        return new CharacterRecord
        {
            ContentId = Character,
            Name = MainName,
            HomeWorldId = HomeWorld,
            Retainers = [new RetainerSetting(Alpha, AlphaName, true), new RetainerSetting(Beta, BetaName, true)],
            Snapshots = [.. snapshots],
        };
    }

    public static CharacterRecord AltOn(uint homeWorldId, params ListingSlot[] listings)
    {
        return new CharacterRecord
        {
            ContentId = Alt,
            Name = AltName,
            HomeWorldId = homeWorldId,
            Retainers = [new RetainerSetting(AltRetainer, "Gamma", true)],
            Snapshots = [new RetainerSnapshot { RetainerId = AltRetainer, CharacterContentId = Alt, ReadAtUtc = Now.AddDays(-1), Town = 1, Gil = 0, Slots = [.. listings] }],
        };
    }

    public static ListingInputs Inputs(params CharacterRecord[] characters)
    {
        return new ListingInputs
        {
            Characters = [.. characters],
            Rules = RuleBook.Create([OreRule()], [], ItemIdSet.Empty, VendorSettings.Off, SellSources.Default, []),
            Pricing = new PricingSettings(),
            Items = [OreFacts],
            References = References(OreReferences(OreReference, WorldLowest)),
            HomeWorldId = HomeWorld,
            NowUtc = Now,
        };
    }

    public static ListingRow Single(ListingTable table, ulong retainerId, byte slot)
    {
        for (var index = 0; index < table.Rows.Length; index++)
        {
            if (table.Rows[index].RetainerId == retainerId && table.Rows[index].Slot == slot)
            {
                return table.Rows[index];
            }
        }

        Assert.Fail($"No listing in slot {slot} of retainer {retainerId}.");
        return default;
    }

    public static ImmutableArray<KeyValuePair<RepriceKey, RepriceEntry>> Repriced(ulong retainerId, byte slot, RepriceEntry entry)
    {
        return [new KeyValuePair<RepriceKey, RepriceEntry>(new RepriceKey(retainerId, slot, NormalOre), entry)];
    }
}
