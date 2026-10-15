using System.Collections.Immutable;
using System.IO;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Characters.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Sales.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Tests.Ledger;

namespace AutoSeller.Tests.Characters;

public sealed class CharacterDataTests : IDisposable
{
    private const ulong Character = 4_000_100;
    private const ulong Other = 4_000_200;
    private const ulong FirstRetainer = 101;
    private const ulong SecondRetainer = 102;
    private const uint Ore = 5_111;
    private const uint Potion = 4_551;
    private const uint HomeWorld = 40;
    private const string CharacterName = "Wren Ashford";

    private readonly TemporaryDirectory directory = new();

    public void Dispose() => directory.Dispose();

    [Fact]
    public void ContentIdZeroIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterData(0));
    }

    [Fact]
    public void UnknownRetainerCountsAsEnabled()
    {
        var data = new CharacterData(Character);

        Assert.True(data.IsEnabled(FirstRetainer));
        Assert.False(data.SetEnabled(FirstRetainer, false));
        Assert.Equal(0, data.Revision);
    }

    [Fact]
    public void RosterAddsRetainersEnabledAndKeepsTheirSwitchAcrossRenames()
    {
        var data = new CharacterData(Character);
        Assert.True(data.UpdateRoster([Retainer(FirstRetainer, "Alpha"), Retainer(SecondRetainer, "Beta")]));
        Assert.True(data.SetEnabled(SecondRetainer, false));

        Assert.True(data.UpdateRoster([Retainer(FirstRetainer, "Alpha"), Retainer(SecondRetainer, "Gamma")]));

        Assert.Equal(new RetainerSetting(SecondRetainer, "Gamma", false), data.Retainers[1]);
        Assert.Equal(new[] { FirstRetainer }, data.EnabledRetainerIds());
    }

    [Fact]
    public void SameRosterChangesNothing()
    {
        var data = new CharacterData(Character);
        data.UpdateRoster([Retainer(FirstRetainer, "Alpha")]);
        var revision = data.Revision;

        Assert.False(data.UpdateRoster([Retainer(FirstRetainer, "Alpha"), Retainer(0, "Ghost"), Retainer(FirstRetainer, "Alpha")]));

        Assert.Equal(revision, data.Revision);
    }

    [Fact]
    public void RetainerLeavingTheRosterTakesItsSnapshotAndApprovalsAlong()
    {
        var data = new CharacterData(Character);
        data.UpdateRoster([Retainer(FirstRetainer, "Alpha"), Retainer(SecondRetainer, "Beta")]);
        data.RecordSnapshot(Snapshot(SecondRetainer, Listing(0, Ore, 1_000)), []);
        data.Approve(new ApprovedPrice(SecondRetainer, 0, Ore, false, 1_000, 800));

        data.UpdateRoster([Retainer(FirstRetainer, "Alpha")]);

        Assert.False(data.TryGetSnapshot(SecondRetainer, out _));
        Assert.Empty(data.Approvals);
        Assert.Empty(data.ToRecord(Now).ListingAges);
    }

    [Fact]
    public void RosterReadThatFindsNoRetainerKeepsTheRosterAndItsData()
    {
        var data = new CharacterData(Character);
        data.UpdateRoster([Retainer(FirstRetainer, "Alpha")]);
        data.RecordSnapshot(Snapshot(FirstRetainer, Listing(0, Ore, 1_000)), []);
        data.Approve(new ApprovedPrice(FirstRetainer, 0, Ore, false, 1_000, 800));
        var revision = data.Revision;

        Assert.False(data.UpdateRoster([]));
        Assert.False(data.UpdateRoster([Retainer(0, "Ghost")]));

        Assert.Single(data.Retainers);
        Assert.True(data.TryGetSnapshot(FirstRetainer, out _));
        Assert.Single(data.Approvals);
        Assert.Equal(revision, data.Revision);
    }

    [Fact]
    public void LatestSnapshotReplacesTheLastOne()
    {
        var data = new CharacterData(Character);
        data.RecordSnapshot(Snapshot(FirstRetainer, Listing(0, Ore, 1_000)), []);
        var latest = Snapshot(FirstRetainer, Listing(0, Potion, 50));

        data.RecordSnapshot(latest, []);

        Assert.True(data.TryGetSnapshot(FirstRetainer, out var kept));
        Assert.Same(latest, kept);
        Assert.Single(data.Snapshots);
    }

    [Fact]
    public void AListingKeepsTheTimeItWasFirstSeenThroughARepriceAndANewOneStartsItsOwn()
    {
        var data = new CharacterData(Character);
        var nextDay = Now.AddDays(1);
        data.RecordSnapshot(Snapshot(FirstRetainer, Listing(0, Ore, 1_000)), []);

        data.RecordSnapshot(SnapshotAt(FirstRetainer, nextDay, Listing(0, Ore, 900), Listing(1, Potion, 50)), []);

        Assert.Equal<ListingAge>(
            [new ListingAge(FirstRetainer, Listing(0, Ore, 900), Now), new ListingAge(FirstRetainer, Listing(1, Potion, 50), nextDay)],
            data.ToRecord(nextDay).ListingAges);
    }

    [Fact]
    public void ALotTheJournalListedInAnEmptiedSlotStartsItsOwnAge()
    {
        var data = new CharacterData(Character);
        var nextDay = Now.AddDays(1);
        data.RecordSnapshot(Snapshot(FirstRetainer, Listing(0, Ore, 1_000)), []);
        SnapshotDiffWrite[] listed = [new(SnapshotDiffWriteKind.Listed, byte.MaxValue, Ore, false, 5, 0, 950, nextDay.AddMinutes(-1))];

        data.RecordSnapshot(SnapshotAt(FirstRetainer, nextDay, Listing(0, Ore, 950)), listed);

        Assert.Equal<ListingAge>([new ListingAge(FirstRetainer, Listing(0, Ore, 950), nextDay)], data.ToRecord(nextDay).ListingAges);
    }

    [Fact]
    public void AListingGoneFromItsSlotTakesItsTimeAlong()
    {
        var data = new CharacterData(Character);
        data.RecordSnapshot(Snapshot(FirstRetainer, Listing(0, Ore, 1_000)), []);
        data.RecordSnapshot(Snapshot(SecondRetainer, Listing(0, Potion, 50)), []);

        data.RecordSnapshot(SnapshotAt(FirstRetainer, Now.AddDays(1)), []);

        Assert.Equal<ListingAge>([new ListingAge(SecondRetainer, Listing(0, Potion, 50), Now)], data.ToRecord(Now).ListingAges);
    }

    [Fact]
    public void SnapshotOfAnotherCharacterIsRefused()
    {
        var data = new CharacterData(Character);

        Assert.Throws<ArgumentException>(() => data.RecordSnapshot(SnapshotOf(Other, FirstRetainer), []));
        Assert.Empty(data.Snapshots);
    }

    [Fact]
    public void ApprovalOutlivesAReadOnlyWhileItsListingIsUnchanged()
    {
        var data = new CharacterData(Character);
        var kept = new ApprovedPrice(FirstRetainer, 0, Ore, false, 1_000, 800);
        var repriced = new ApprovedPrice(FirstRetainer, 1, Potion, false, 90, 70);
        var otherRetainer = new ApprovedPrice(SecondRetainer, 0, Ore, false, 1_000, 800);
        data.Approve(kept);
        data.Approve(repriced);
        data.Approve(otherRetainer);

        data.RecordSnapshot(Snapshot(FirstRetainer, Listing(0, Ore, 1_000), Listing(1, Potion, 85)), []);

        Assert.Equal([kept, otherRetainer], data.Approvals);
    }

    [Fact]
    public void NewApprovalReplacesTheOneForTheSameListing()
    {
        var data = new CharacterData(Character);
        data.Approve(new ApprovedPrice(FirstRetainer, 0, Ore, false, 1_000, 800));

        data.Approve(new ApprovedPrice(FirstRetainer, 0, Ore, false, 1_000, 750));

        Assert.Equal(750u, Assert.Single(data.Approvals).Price);
    }

    [Fact]
    public void AWithdrawnApprovalIsGoneAndAMissingOneChangesNothing()
    {
        var data = new CharacterData(Character);
        var approval = new ApprovedPrice(FirstRetainer, 0, Ore, false, 1_000, 800);
        data.Approve(approval);
        var revision = data.Revision;

        Assert.True(data.Withdraw(approval with { Price = 750 }));
        Assert.False(data.Withdraw(approval));

        Assert.Empty(data.Approvals);
        Assert.Equal(revision + 1, data.Revision);
    }

    [Fact]
    public void TheCharactersNameAndHomeWorldAreKeptOnlyWhenTheyChange()
    {
        var data = new CharacterData(Character);

        Assert.False(data.Describe(string.Empty, HomeWorld));
        Assert.False(data.Describe(CharacterName, 0));
        Assert.True(data.Describe(CharacterName, HomeWorld));
        Assert.False(data.Describe(CharacterName, HomeWorld));

        Assert.Equal(CharacterName, data.Name);
        Assert.Equal(HomeWorld, data.HomeWorldId);
        Assert.Equal(1, data.Revision);
    }

    [Fact]
    public void RepriceIsRecordedAndReadBack()
    {
        var data = new CharacterData(Character);
        var key = new RepriceKey(FirstRetainer, 0, new ItemKey(Ore, false));

        data.RecordReprice(key, Now, 1_000, 900);

        var entry = data.RepriceEntryFor(key);
        Assert.Equal(Now, entry.LastRepriceUtc);
        Assert.Equal(1, entry.DropCount);
        Assert.Equal(1, data.Revision);
    }

    [Fact]
    public void PlanEditsAreKept()
    {
        var data = new CharacterData(Character);
        var row = new PlanRowKey(FirstRetainer, ListingSlot.MarketContainer, 0, 0, Ore, false);
        PlanEdits.None.TryApply(PlanEdit.Exclude(row, Now), new PlanEditTarget(row, 0, 0), out var edits);

        data.SetPlanEdits(edits);
        data.SetPlanEdits(edits);

        Assert.True(data.PlanEdits.IsExcluded(row));
        Assert.Equal(1, data.Revision);
    }

    [Fact]
    public void RecordKeepsEverythingAcrossASaveAndALoad()
    {
        var data = Filled();
        var store = new CheckpointStore<CharacterRecord>(Path.Combine(directory.Path, CharacterRecord.FolderName), CharacterRecord.FileNameFor(Character));

        Assert.True(store.TrySave(data.ToRecord(Now)));
        var load = store.Load();

        Assert.Equal(CheckpointSource.Primary, load.Source);
        var restored = CharacterData.FromRecord(load.Value!);
        Assert.Equal(Character, restored.ContentId);
        Assert.Equal(CharacterName, restored.Name);
        Assert.Equal(HomeWorld, restored.HomeWorldId);
        Assert.Equal(data.Retainers, restored.Retainers);
        Assert.False(restored.IsEnabled(SecondRetainer));
        Assert.True(restored.TryGetSnapshot(FirstRetainer, out var snapshot));
        Assert.Equal(Now, snapshot!.ReadAtUtc);
        Assert.Equal(DateTimeKind.Utc, snapshot.ReadAtUtc.Kind);
        Assert.Equal(new[] { Listing(0, Ore, 1_000), Listing(3, Potion, 60) }, snapshot.Slots);
        Assert.Equal(data.Approvals, restored.Approvals);
        Assert.Equal(2, restored.ToRecord(Now).ListingAges.Length);
        Assert.Equal<ListingAge>(data.ToRecord(Now).ListingAges, restored.ToRecord(Now).ListingAges);
        Assert.Equal<PlanEdit>(data.PlanEdits.Items, restored.PlanEdits.Items);
        var key = new RepriceKey(FirstRetainer, 0, new ItemKey(Ore, false));
        Assert.Equal(data.RepriceEntryFor(key).LastRepriceUtc, restored.RepriceEntryFor(key).LastRepriceUtc);
        Assert.Equal<DateTime>(data.RepriceEntryFor(key).DropTimesUtc, restored.RepriceEntryFor(key).DropTimesUtc);
        Assert.Equal(data.Streaks, restored.Streaks);
        Assert.Equal(0, restored.Revision);
    }

    [Fact]
    public void RecordOfThisCharacterAtThisVersionFits()
    {
        Assert.Equal(CharacterRecordFit.Fits, CharacterRecord.FitFor(new CharacterRecord { ContentId = Character }, Character));
        Assert.Equal(CharacterRecordFit.Fits, CharacterRecord.FitFor(new CharacterRecord { ContentId = Character, Version = CharacterRecord.CurrentVersion - 1 }, Character));
    }

    [Fact]
    public void RecordThatDoesNotFitSaysWhy()
    {
        Assert.Equal(CharacterRecordFit.Missing, CharacterRecord.FitFor(null, Character));
        Assert.Equal(CharacterRecordFit.OtherCharacter, CharacterRecord.FitFor(new CharacterRecord { ContentId = Other }, Character));
        Assert.Equal(CharacterRecordFit.NewerVersion, CharacterRecord.FitFor(new CharacterRecord { ContentId = Character, Version = CharacterRecord.CurrentVersion + 1 }, Character));
    }

    [Fact]
    public void SetAsideCopyIsNamedAfterItsVersionAndTime()
    {
        Assert.Equal(".v3-20260314-090507", CharacterRecord.SetAsideSuffix(3, new DateTime(2026, 3, 14, 9, 5, 7, DateTimeKind.Utc)));
    }

    [Fact]
    public void NewerFileIsCopiedAsideBeforeTheFirstSaveReplacesIt()
    {
        var store = new CheckpointStore<CharacterRecord>(Path.Combine(directory.Path, CharacterRecord.FolderName), CharacterRecord.FileNameFor(Character));
        Assert.True(store.TrySave(new CharacterRecord { ContentId = Character, Version = CharacterRecord.CurrentVersion + 1 }));
        var load = store.Load();
        Assert.Equal(CharacterRecordFit.NewerVersion, CharacterRecord.FitFor(load.Value, Character));

        Assert.True(store.TryKeepCopy(load.Source, CharacterRecord.SetAsideSuffix(load.Value!.Version, Now), out var copyPath));
        Assert.True(store.TrySave(new CharacterData(Character).ToRecord(Now)));
        Assert.True(store.TrySave(new CharacterData(Character).ToRecord(Now)));

        var kept = new CheckpointStore<CharacterRecord>(Path.GetDirectoryName(copyPath)!, Path.GetFileName(copyPath)!).Load().Value;
        Assert.Equal(CharacterRecord.CurrentVersion + 1, kept!.Version);
    }

    [Fact]
    public void FileIsNamedAfterTheContentId()
    {
        Assert.Equal("4000100.json", CharacterRecord.FileNameFor(Character));
    }

    [Fact]
    public void SavingDropsRepricesOlderThanAnyRuleLooksBack()
    {
        var data = new CharacterData(Character);
        var old = new RepriceKey(FirstRetainer, 0, new ItemKey(Ore, false));
        var recent = new RepriceKey(FirstRetainer, 1, new ItemKey(Potion, false));
        data.RecordReprice(old, Now - CharacterData.RepriceRetention, 100, 90);
        data.RecordReprice(recent, Now - CharacterData.RepriceRetention + TimeSpan.FromMinutes(1), 100, 90);

        var record = data.ToRecord(Now);

        Assert.Equal(recent, Assert.Single(record.Reprices).Key);
        Assert.Equal(2, data.RepriceCount);
    }

    [Fact]
    public void RetentionCoversTheLongestCooldownAndPriceWarWindow()
    {
        Assert.True(CharacterData.RepriceRetention >= PricingSettings.RepriceCooldownRange.Maximum);
        Assert.True(CharacterData.RepriceRetention >= PricingSettings.PriceWarWindowRange.Maximum);
    }

    [Fact]
    public void HandEditedRecordLoadsWhatItCan()
    {
        var record = new CharacterRecord
        {
            ContentId = Character,
            Name = null!,
            Retainers = [new RetainerSetting(0, "Nobody", true), new RetainerSetting(FirstRetainer, "Alpha", false), new RetainerSetting(FirstRetainer, "Again", true)],
            Snapshots = [SnapshotOf(Other, SecondRetainer), Snapshot(FirstRetainer)],
            Reprices = default,
            Approvals = default,
            PlanEdits = default,
        };

        var data = CharacterData.FromRecord(record);

        Assert.Equal([new RetainerSetting(FirstRetainer, "Alpha", false)], data.Retainers);
        Assert.Equal(string.Empty, data.Name);
        Assert.Single(data.Snapshots);
        Assert.Empty(data.Approvals);
        Assert.Same(PlanEdits.None, data.PlanEdits);
        Assert.Equal(0, data.RepriceCount);
    }

    [Fact]
    public void ABelowFloorDecisionStartsAStreakThatAJudgedMarketEnds()
    {
        var data = new CharacterData(Character);
        data.RecordSnapshot(Snapshot(FirstRetainer, Listing(0, Ore, 1_000)), []);
        var key = new RepriceKey(FirstRetainer, 0, new ItemKey(Ore, false));

        data.TrackDecision(key, Decided(DecisionReason.BelowFloorKept), Now);
        data.TrackDecision(key, Decided(DecisionReason.UnknownCompetition), Now.AddDays(1));
        Assert.Equal(TimeSpan.FromDays(3), data.StreakFor(key).BelowFloorFor(Now.AddDays(3)));

        data.TrackDecision(key, Decided(DecisionReason.Undercut), Now.AddDays(3));
        Assert.Empty(data.Streaks);
    }

    [Fact]
    public void TheLowestPriceStreakFollowsTheWatchedWorldPrice()
    {
        var data = new CharacterData(Character);
        var key = new RepriceKey(FirstRetainer, 0, new ItemKey(Ore, false));

        data.TrackLowestPrice(key, 1_000, Now.AddHours(-2), new LowestPriceCheck(Now.AddHours(-1), 1_000));
        data.TrackLowestPrice(key, 1_000, Now.AddHours(-2), new LowestPriceCheck(Now, 1_200));
        Assert.Equal(Now.AddHours(-1), data.StreakFor(key).LowestSinceUtc);

        data.TrackLowestPrice(key, 1_000, Now.AddHours(-2), new LowestPriceCheck(Now.AddHours(1), 900));
        Assert.Equal(default, data.StreakFor(key).LowestSinceUtc);
    }

    [Fact]
    public void AStreakOnlyChangesTheRevisionWhenItChanges()
    {
        var data = new CharacterData(Character);
        var key = new RepriceKey(FirstRetainer, 0, new ItemKey(Ore, false));
        data.TrackDecision(key, Decided(DecisionReason.BelowFloorKept), Now);
        var revision = data.Revision;

        data.TrackDecision(key, Decided(DecisionReason.BelowFloorKept), Now.AddHours(1));
        data.TrackDecision(new RepriceKey(FirstRetainer, 1, new ItemKey(Ore, false)), Decided(DecisionReason.AlreadyLowest), Now);

        Assert.Equal(revision, data.Revision);
    }

    [Fact]
    public void AListingGoneFromItsSlotTakesItsStreakAlong()
    {
        var data = new CharacterData(Character);
        data.RecordSnapshot(Snapshot(FirstRetainer, Listing(0, Ore, 1_000), Listing(1, Potion, 50)), []);
        var gone = new RepriceKey(FirstRetainer, 0, new ItemKey(Ore, false));
        var kept = new RepriceKey(FirstRetainer, 1, new ItemKey(Potion, false));
        data.TrackDecision(gone, Decided(DecisionReason.BelowFloorKept), Now);
        data.TrackDecision(kept, Decided(DecisionReason.BelowFloorKept), Now);

        data.RecordSnapshot(SnapshotAt(FirstRetainer, Now.AddDays(1), Listing(1, Potion, 45)), []);

        Assert.Equal([new ListingStreak(kept, default, Now)], data.Streaks);
    }

    private static Decision Decided(DecisionReason reason)
    {
        return new Decision(SellAction.Keep, 1_000, 400, reason, new DecisionEvidence(0, 0, 0, 0, ReferenceSource.None, 0, 500));
    }

    private static CharacterData Filled()
    {
        var data = new CharacterData(Character);
        data.Describe(CharacterName, HomeWorld);
        data.UpdateRoster([Retainer(FirstRetainer, "Alpha"), Retainer(SecondRetainer, "Beta")]);
        data.SetEnabled(SecondRetainer, false);
        data.RecordSnapshot(Snapshot(FirstRetainer, Listing(0, Ore, 1_000), Listing(3, Potion, 60)), []);
        data.Approve(new ApprovedPrice(FirstRetainer, 0, Ore, false, 1_000, 700));
        data.RecordReprice(new RepriceKey(FirstRetainer, 0, new ItemKey(Ore, false)), Now - TimeSpan.FromHours(3), 1_200, 1_000);
        data.RecordReprice(new RepriceKey(FirstRetainer, 0, new ItemKey(Ore, false)), Now - TimeSpan.FromHours(1), 1_100, 1_000);
        data.TrackDecision(new RepriceKey(FirstRetainer, 3, new ItemKey(Potion, false)), Decided(DecisionReason.BelowFloorKept), Now - TimeSpan.FromDays(2));
        var row = new PlanRowKey(FirstRetainer, ListingSlot.MarketContainer, 3, 0, Potion, false);
        PlanEdits.None.TryApply(PlanEdit.Price(row, 75, Now), new PlanEditTarget(row, 10, 0), out var edits);
        data.SetPlanEdits(edits);
        return data;
    }

    private static RetainerInfo Retainer(ulong retainerId, string name)
    {
        return new RetainerInfo(retainerId, name, 1, 0, 0, true);
    }

    private static ListingSlot Listing(byte slot, uint itemId, uint unitPrice)
    {
        return new ListingSlot(slot, itemId, false, 5, unitPrice);
    }

    private static RetainerSnapshot Snapshot(ulong retainerId, params ListingSlot[] listings)
    {
        return SnapshotAt(retainerId, Now, listings);
    }

    private static RetainerSnapshot SnapshotAt(ulong retainerId, DateTime readAtUtc, params ListingSlot[] listings)
    {
        return new RetainerSnapshot
        {
            RetainerId = retainerId,
            CharacterContentId = Character,
            ReadAtUtc = readAtUtc,
            Town = 1,
            Gil = 10_000,
            Slots = ImmutableArray.Create(listings),
        };
    }

    private static RetainerSnapshot SnapshotOf(ulong contentId, ulong retainerId)
    {
        return new RetainerSnapshot
        {
            RetainerId = retainerId,
            CharacterContentId = contentId,
            ReadAtUtc = Now,
            Town = 1,
            Gil = 0,
            Slots = [],
        };
    }
}
