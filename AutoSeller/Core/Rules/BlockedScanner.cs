using System.Collections.Immutable;
using AutoSeller.Core.Characters;
using AutoSeller.Core.Inventory;
using AutoSeller.Core.Items;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;

namespace AutoSeller.Core.Rules;

internal readonly record struct BlockedRow(BlockedItem Item, string Name, ushort IconId);

internal sealed class BlockedScan
{
    public static readonly BlockedScan None = new() { Rows = [], LoggedIn = false, Revision = 0, ContentId = 0 };

    public required ImmutableArray<BlockedRow> Rows { get; init; }

    public required bool LoggedIn { get; init; }

    public required int Revision { get; init; }

    public required ulong ContentId { get; init; }
}

internal sealed class BlockedScanner : IDisposable
{
    private readonly Configuration configuration;
    private readonly BagReader bags;
    private readonly GameItems items;
    private readonly CharacterStore characters;
    private volatile bool requested;
    private volatile BlockedScan latest = BlockedScan.None;
    private int revision;

    public BlockedScanner(Configuration configuration, BagReader bags, GameItems items, CharacterStore characters)
    {
        this.configuration = configuration;
        this.bags = bags;
        this.items = items;
        this.characters = characters;
        Svc.Framework.Update += OnUpdate;
    }

    public BlockedScan Latest => latest;

    public ulong CurrentContentId => characters.Current?.ContentId ?? 0;

    public bool Pending => requested;

    public void Dispose()
    {
        Svc.Framework.Update -= OnUpdate;
    }

    public void Request()
    {
        requested = true;
    }

    private void OnUpdate(IFramework framework)
    {
        if (!requested)
        {
            return;
        }

        requested = false;
        revision++;
        latest = Scan();
    }

    private BlockedScan Scan()
    {
        var contentId = CurrentContentId;
        if (!Svc.ClientState.IsLoggedIn)
        {
            return new BlockedScan { Rows = [], LoggedIn = false, Revision = revision, ContentId = contentId };
        }

        var rules = configuration.BuildRuleBook();
        var read = bags.Read(rules.Sources, configuration.InventoryRetainerIds);
        var stock = ImmutableArray.CreateBuilder<StockSlot>(read.Slots.Length);
        for (var index = 0; index < read.Slots.Length; index++)
        {
            var slot = read.Slots[index];
            stock.Add(new StockSlot(slot, items.FactsOf(slot.ItemId), 0));
        }

        var blocked = BlockedItems.Find(stock.MoveToImmutable(), rules, ListedOnRetainers(), read.FreeBagSlots);
        var rows = ImmutableArray.CreateBuilder<BlockedRow>(blocked.Length);
        for (var index = 0; index < blocked.Length; index++)
        {
            var item = items.Get(blocked[index].Slot.ItemId);
            rows.Add(new BlockedRow(blocked[index], item.Name, item.IconId));
        }

        RunLog.Info($"Rules check: {read.Slots.Length} stacks read, {blocked.Length} matched stacks blocked.");
        return new BlockedScan { Rows = rows.MoveToImmutable(), LoggedIn = true, Revision = revision, ContentId = contentId };
    }

    private ListingCounts ListedOnRetainers()
    {
        var current = characters.Current;
        if (current is null)
        {
            return ListingCounts.Empty;
        }

        var snapshots = new RetainerSnapshot[current.Snapshots.Count];
        for (var index = 0; index < snapshots.Length; index++)
        {
            snapshots[index] = current.Snapshots[index];
        }

        return ListingCounts.From(snapshots);
    }
}
