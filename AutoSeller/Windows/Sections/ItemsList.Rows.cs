using System.Globalization;
using System.Text;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Items;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Tasks;
using AutoSeller.Core.Tasks.Model;
using AutoSeller.Windows.Components;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal sealed partial class ItemsList
{
    private const long TextRefreshMs = 60_000;
    private const int MaximumBadges = 4;
    private const string ChipIdPrefix = "##asell_items_retainer_chip_";

    private static readonly SellSources[] sourceOrder = [SellSources.Bags, SellSources.Crystals, SellSources.Saddlebag, SellSources.Armoury, SellSources.RetainerPages];

    private readonly StringBuilder tooltip = new();
    private readonly string[] badgeLabels = new string[MaximumBadges];
    private readonly Vector4[] badgeColors = new Vector4[MaximumBadges];
    private string marketLabel = string.Empty;
    private string npcLabel = string.Empty;
    private string autoLabel = string.Empty;
    private string offLabel = string.Empty;
    private string offHelp = string.Empty;
    private string myPriceLabel = string.Empty;
    private string marketHelp = string.Empty;
    private string npcHelp = string.Empty;
    private string undercutHelp = string.Empty;
    private string myPriceHelp = string.Empty;
    private string retainersTitle = string.Empty;
    private string listingsTitle = string.Empty;
    private string sellTitle = string.Empty;
    private string listingsStatus = string.Empty;
    private Vector4 listingsStatusColor;
    private bool listingsAttention;
    private string bagsStatus = string.Empty;
    private Vector4 bagsStatusColor;
    private bool noSlotWaiting;
    private string offRetainers = string.Empty;
    private RetainerChip[] retainerChips = [];
    private RowText[] rows = [];
    private int rowCount;
    private int listingCount;
    private int[] shown = [];
    private int shownCount;
    private int[] shownListings = [];
    private int shownListingCount;
    private string search = string.Empty;
    private bool searchFocused;
    private string openId = string.Empty;
    private string worth = string.Empty;
    private string columnItem = string.Empty;
    private string columnWhere = string.Empty;
    private string columnRetainer = string.Empty;
    private string columnYourPrice = string.Empty;
    private string columnLowest = string.Empty;
    private string columnMarket = string.Empty;
    private string columnNpc = string.Empty;
    private string columnPricing = string.Empty;
    private string columnSellOn = string.Empty;
    private string columnStatus = string.Empty;
    private string[] retainerLabels = [];
    private ulong[] retainerIds = [];
    private LanguageInfo? labelsLanguage;
    private TextKey key;

    private enum RowKind : byte
    {
        Listing,
        Bag,
    }

    private readonly record struct RetainerChip(string Id, ulong RetainerId, string Name, string Count, string Tooltip, bool Enabled);

    private readonly record struct TextKey(long Generation, LanguageInfo? Language, bool Editable, long Step, Guid RunId, int Decided, bool Live);

    private sealed class RowText
    {
        public RowKind Kind;
        public PlanRowKey Key;
        public BagItem Item;
        public string OpenId = string.Empty;
        public string FirstId = string.Empty;
        public string SecondId = string.Empty;
        public string ThirdId = string.Empty;
        public string PriceId = string.Empty;
        public string AmountId = string.Empty;
        public string RetainerId = string.Empty;
        public string ApproveId = string.Empty;
        public string Name = string.Empty;
        public string Note = string.Empty;
        public string Where = string.Empty;
        public string PriceNow = string.Empty;
        public string PriceNext = string.Empty;
        public string Second = string.Empty;
        public string Status = string.Empty;
        public string Tooltip = string.Empty;
        public string Why = string.Empty;
        public string PriceHint = string.Empty;
        public string PriceTooltip = string.Empty;
        public string AmountHint = string.Empty;
        public string AmountOf = string.Empty;
        public string ApproveTooltip = string.Empty;
        public string[] Badges = [];
        public Vector4[] BadgeColors = [];
        public Vector4 NoteColor;
        public Vector4 NowColor;
        public Vector4 NextColor;
        public Vector4 SecondColor;
        public Vector4 StatusColor;
        public Vector4 Accent;
        public uint CustomPrice;
        public uint ApproveFrom;
        public uint ApprovePrice;
        public uint VendorUnit;
        public ushort IconId;
        public bool Changes;
        public bool Muted;
        public bool HighQuality;
        public bool CanBeHq;
        public bool Manageable;
        public bool Excluded;
        public bool PriceSet;
        public bool IsCheapest;
        public bool Approvable;
    }

    private void Refresh(Plugin plugin, SellPlan plan, in RunView run)
    {
        var next = new TextKey(plan.Generation, Loc.Current, plugin.Plan.CanEdit, Environment.TickCount64 / TextRefreshMs, run.RunId, run.Decisions.Length, run.Live);
        if (next == key)
        {
            return;
        }

        key = next;
        BuildLabels();
        BuildRetainerChips(plan);
        var input = plan.Input;
        var items = BagItems.From(input.Stock.AsSpan(), input.Rules, input.Pricing, input.References, input.HomeWorldId, input.NowUtc);
        var planRows = plan.Rows;
        var capacity = planRows.Length + items.Length;
        if (rows.Length < capacity)
        {
            rows = new RowText[capacity];
            shown = new int[capacity];
            shownListings = new int[capacity];
        }

        rowCount = 0;
        noSlotWaiting = false;
        for (var index = 0; index < planRows.Length; index++)
        {
            if (planRows[index].IsListing)
            {
                rows[rowCount++] = BuildListing(plugin.GameItems, plan, planRows[index], index);
            }
        }

        listingCount = rowCount;
        for (var index = 0; index < items.Length; index++)
        {
            rows[rowCount++] = BuildBag(plugin.GameItems, plan, items[index], planRows.AsSpan());
        }

        if (run.Live)
        {
            MarkProgress(run.Decisions);
        }

        BuildRetainerChoices(plan);
        BuildListingsSummary(plugin.Configuration.UndercutAllListings);
        BuildBagsSummary();
        listingsTab = new BoardTab(listingsTitle, PlanLabels.Count(listingCount), listingsStatus, listingsStatusColor);
        bagsTab = new BoardTab(sellTitle, PlanLabels.Count(rowCount - listingCount), bagsStatus, bagsStatusColor);
        var estimate = BagTotals.Of(items.AsSpan()).Estimate;
        worth = estimate > 0 ? Loc.T(L.Sell.ListWorth, Formatting.Gil(estimate)) : string.Empty;
        ApplyFilter();
    }

    private void BuildLabels()
    {
        if (ReferenceEquals(labelsLanguage, Loc.Current))
        {
            return;
        }

        labelsLanguage = Loc.Current;
        listingsTitle = Loc.T(L.Sell.ListingsTitle);
        sellTitle = Loc.T(L.Sell.SellTitle);
        marketLabel = Loc.T(L.Sell.ButtonMarket);
        npcLabel = Loc.T(L.Sell.ButtonNpc);
        autoLabel = Loc.T(L.Sell.ButtonAuto);
        offLabel = Loc.T(L.Sell.ButtonOff);
        offHelp = Loc.T(L.Sell.ButtonOffHelp);
        myPriceLabel = Loc.T(L.Sell.ButtonMyPrice);
        marketHelp = Loc.T(L.Sell.ButtonMarketHelp);
        npcHelp = Loc.T(L.Sell.ButtonNpcHelp);
        undercutHelp = Loc.T(L.Sell.ButtonUndercutHelp);
        myPriceHelp = Loc.T(L.Sell.ButtonMyPriceHelp);
        retainersTitle = Loc.T(L.Sell.RetainersTitle);
        columnItem = TextDraw.Upper(Loc.T(L.Sell.ColumnItem));
        columnWhere = TextDraw.Upper(Loc.T(L.Sell.ColumnWhere));
        columnRetainer = TextDraw.Upper(Loc.T(L.Sell.ColumnRetainer));
        columnYourPrice = TextDraw.Upper(Loc.T(L.Sell.ColumnYourPrice));
        columnLowest = TextDraw.Upper(Loc.T(L.Sell.ColumnLowest));
        columnMarket = TextDraw.Upper(Loc.T(L.Sell.ColumnMarket));
        columnNpc = TextDraw.Upper(Loc.T(L.Sell.ColumnNpc));
        columnPricing = TextDraw.Upper(Loc.T(L.Sell.ColumnPricing));
        columnSellOn = TextDraw.Upper(Loc.T(L.Rules.ColumnSellOn));
        columnStatus = TextDraw.Upper(Loc.T(L.Sell.ColumnStatus));
    }

    private RowText BuildListing(GameItems items, SellPlan plan, in PlanRow row, int index)
    {
        var item = items.Get(row.Key.ItemId);
        var suffix = string.Concat("L", index.ToString(CultureInfo.InvariantCulture));
        var excluded = row.Has(PlanRowEdits.Excluded);
        var priceSet = row.Has(PlanRowEdits.PriceSet);
        var manageable = row.Duty != ListingDuty.NotManaged || excluded;
        var approved = ApprovedPriceOf(plan, row);
        var approvable = row.NeedsApproval && row.Decision.ProposedPrice != approved && row.CanApprove(row.CurrentPrice, row.Decision.ProposedPrice);
        plan.TryGetRetainer(row.RetainerId, out var retainer);
        var evidence = row.Decision.Evidence;
        var text = new RowText
        {
            Kind = RowKind.Listing,
            Key = row.Key,
            OpenId = string.Concat("##asell_items_open_", suffix),
            FirstId = string.Concat("##asell_items_auto_", suffix),
            SecondId = string.Concat("##asell_items_myprice_", suffix),
            ThirdId = string.Concat("##asell_items_off_", suffix),
            PriceId = string.Concat("##asell_items_price_", suffix),
            ApproveId = string.Concat("##asell_items_approve_", suffix),
            Name = string.Concat(PlanLabels.ItemName(items, row.Key.ItemId), " ", Loc.T(L.Sell.ItemsQuantity, PlanLabels.Count(row.Quantity))),
            Where = retainer.Name,
            PriceNow = PlanLabels.Gil(row.CurrentPrice),
            NowColor = excluded || !manageable ? Styling.TextMuted : Styling.TextSecondary,
            IconId = item.IconId,
            HighQuality = row.Key.IsHq,
            Manageable = manageable,
            Muted = excluded || !manageable,
            Excluded = excluded,
            PriceSet = priceSet,
            IsCheapest = !excluded && manageable && row.Decision.Reason == DecisionReason.AlreadyLowest,
            CustomPrice = priceSet ? row.Decision.Price : 0,
            Approvable = approvable,
            ApproveFrom = row.CurrentPrice,
            ApprovePrice = row.Decision.ProposedPrice,
            ApproveTooltip = approvable ? Loc.T(L.Plan.ApproveHelp, PlanLabels.Gil(row.CurrentPrice), PlanLabels.Gil(row.Decision.ProposedPrice)) : string.Empty,
            PriceHint = (PlanRow.WritesAPrice(row.Decision.Action) ? row.Decision.Price : row.CurrentPrice).ToString("N0", Loc.Culture),
            PriceTooltip = Loc.T(L.Sell.DetailPriceHelp, PlanLabels.Gil(row.Floor)),
            Why = WhyOf(evidence.CompetitorPrice, row.Floor),
            Accent = approvable ? Styling.AccentAmber : Styling.AccentMint,
        };

        var competitor = evidence.CompetitorPrice > 0 ? evidence.CompetitorPrice : row.UndercutPrice;
        text.Second = competitor > 0 ? PlanLabels.Gil(competitor) : PlanLabels.NoValue;
        text.SecondColor = competitor > 0 && competitor < row.CurrentPrice ? Styling.AccentAmberSoft : Styling.TextDim;
        (text.Note, text.NoteColor) = ListingNote(row, excluded, manageable, priceSet, approvable, competitor);
        if (!excluded && row.Writes)
        {
            text.PriceNext = PlanLabels.Gil(row.Decision.Price);
            text.NextColor = row.Decision.Action == SellAction.RepriceUp ? Styling.AccentBlueSoft : Styling.AccentMintSoft;
            text.Changes = true;
        }
        else if (!excluded && row.Decision.Action == SellAction.Return)
        {
            text.PriceNext = PlanLabels.Action(SellAction.Return);
            text.NextColor = PlanLabels.ActionColor(SellAction.Return);
            text.Changes = true;
        }
        else if (approvable)
        {
            text.PriceNext = PlanLabels.Gil(row.Decision.ProposedPrice);
            text.NextColor = Styling.AccentAmberSoft;
            text.Changes = true;
        }

        text.Status = excluded || !manageable ? PlanLabels.NoValue : Loc.T(L.Sell.StatusWaiting);
        text.StatusColor = Styling.TextMuted;
        ListingBadges(row, text, approved);
        text.Tooltip = TooltipOf(text.Name, text.Note, text.Why);
        return text;
    }

    private RowText BuildBag(GameItems items, SellPlan plan, in BagItem item, ReadOnlySpan<PlanRow> planRows)
    {
        var game = items.Get(item.ItemId);
        var suffix = string.Concat("B", item.ItemId.ToString(CultureInfo.InvariantCulture));
        var vendorUnit = items.FactsOf(item.ItemId).VendorPrice;
        var channel = item.Channel;
        var text = new RowText
        {
            Kind = RowKind.Bag,
            Item = item,
            OpenId = string.Concat("##asell_items_open_", suffix),
            FirstId = string.Concat("##asell_items_market_", suffix),
            SecondId = string.Concat("##asell_items_npc_", suffix),
            PriceId = string.Concat("##asell_items_price_", suffix),
            AmountId = string.Concat("##asell_items_amount_", suffix),
            RetainerId = string.Concat("##asell_items_retainer_", suffix),
            Name = string.Concat(game.Name, " ", QuantityText(item)),
            Where = SourcesText(item.Sources),
            IconId = game.IconId,
            CanBeHq = game.CanBeHq,
            HighQuality = item.HighQualityQuantity == item.Quantity,
            Manageable = true,
            VendorUnit = vendorUnit,
            AmountHint = item.Quantity.ToString("N0", Loc.Culture),
            AmountOf = Loc.T(L.Sell.ItemsAmountOf, item.Quantity.ToString("N0", Loc.Culture)),
            PriceHint = item.Price.Best > 0 ? item.Price.Best.ToString("N0", Loc.Culture) : Loc.T(L.Sell.ItemsNoPrice),
            PriceTooltip = item.Price.Best > 0
                ? Loc.T(L.Sell.ItemsPriceHelp, Formatting.Gil(item.Price.Best), Formatting.Gil(item.Price.Floor))
                : Loc.T(L.Sell.ItemsPriceHelpNoBest, Formatting.Gil(item.Price.Floor)),
            Changes = channel != SellChannel.None,
            Accent = channel == SellChannel.Vendor ? Styling.AccentCoffee : Styling.AccentMint,
            Status = channel == SellChannel.None ? PlanLabels.NoValue : Loc.T(L.Sell.StatusWaiting),
            StatusColor = Styling.TextMuted,
        };

        text.PriceNow = item.CanList && item.Price.Known ? Formatting.Gil(item.Price.Listed) : PlanLabels.NoValue;
        text.Second = vendorUnit > 0 ? Formatting.Gil(vendorUnit) : PlanLabels.NoValue;
        switch (channel)
        {
            case SellChannel.MarketBoard:
                var warning = item.CanList && item.Price.BelowFloor ? Loc.T(L.Sell.ItemsBelowFloor, Formatting.Gil(item.Price.Floor)) : PlacementNoteOf(planRows, item.ItemId);
                noSlotWaiting |= warning.Length > 0 && offRetainers.Length > 0 && IsNoSlot(planRows, item.ItemId);
                text.Note = warning.Length > 0 ? warning : ListingPlanOf(plan, planRows, item.ItemId);
                text.NoteColor = warning.Length > 0 ? Styling.AccentAmberSoft : Styling.AccentMintSoft;
                text.NowColor = Styling.AccentMintSoft;
                text.SecondColor = Styling.TextDim;
                break;
            case SellChannel.Vendor:
                text.Note = Loc.T(L.Sell.NoteSoldToNpc);
                text.NoteColor = Styling.AccentCoffee;
                text.NowColor = Styling.TextDim;
                text.SecondColor = Styling.AccentCoffee;
                break;
            default:
                text.Note = item.CanList ? string.Empty : Loc.T(L.Sell.ItemsCannotList);
                text.NoteColor = Styling.TextDim;
                text.NowColor = Styling.TextSecondary;
                text.SecondColor = Styling.TextSecondary;
                break;
        }

        text.Tooltip = TooltipOf(text.Name, text.Note, item.CanList ? text.PriceTooltip : string.Empty);
        return text;
    }

    private void MarkProgress(ReadOnlySpan<RunDecision> decisions)
    {
        for (var index = 0; index < rowCount; index++)
        {
            var row = rows[index];
            if (!TryFindDecision(decisions, row, out var decision))
            {
                continue;
            }

            row.Status = PlanLabels.Done(decision);
            row.StatusColor = PlanLabels.DoneColor(decision);
        }
    }

    private static bool TryFindDecision(ReadOnlySpan<RunDecision> decisions, RowText row, out RunDecision found)
    {
        for (var index = 0; index < decisions.Length; index++)
        {
            var decision = decisions[index];
            var matches = row.Kind == RowKind.Listing
                ? decision.Kind == RunRowKind.Listing && decision.Row == row.Key
                : decision.Kind != RunRowKind.Listing && decision.Row.ItemId == row.Item.ItemId;
            if (matches)
            {
                found = decision;
                return true;
            }
        }

        found = default;
        return false;
    }

    private void ApplyFilter()
    {
        shownCount = 0;
        shownListingCount = 0;
        var query = search.Trim();
        for (var index = 0; index < rowCount; index++)
        {
            if (query.Length > 0 && !rows[index].Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            if (index < listingCount)
            {
                shownListings[shownListingCount++] = index;
            }
            else
            {
                shown[shownCount++] = index;
            }
        }
    }

    private void BuildBagsSummary()
    {
        var picked = 0;
        for (var index = listingCount; index < rowCount; index++)
        {
            if (rows[index].Changes)
            {
                picked++;
            }
        }

        bagsStatus = picked > 0 ? Loc.Plural(L.Sell.BagsPicked, picked) : Loc.T(L.Sell.BagsPickHint);
        bagsStatusColor = noSlotWaiting ? Styling.AccentAmberSoft : picked > 0 ? Styling.AccentMintSoft : Styling.TextDim;
    }

    private static bool IsNoSlot(ReadOnlySpan<PlanRow> planRows, uint itemId)
    {
        for (var index = 0; index < planRows.Length; index++)
        {
            var row = planRows[index];
            if (row.Kind == RunRowKind.Lot && row.Key.ItemId == itemId && !row.Has(PlanRowEdits.Excluded) && !row.IsPlaced && row.Placement == AllocationReason.NoFreeSlot)
            {
                return true;
            }
        }

        return false;
    }

    private void BuildListingsSummary(bool undercutAllListings)
    {
        var needsOk = 0;
        var changes = 0;
        var cheapest = 0;
        var leftAlone = 0;
        for (var index = 0; index < listingCount; index++)
        {
            var row = rows[index];
            if (row.Approvable)
            {
                needsOk++;
            }
            else if (row.Changes)
            {
                changes++;
            }
            else if (row.Muted)
            {
                leftAlone++;
            }
            else if (row.IsCheapest)
            {
                cheapest++;
            }
        }

        listingsAttention = needsOk > 0 || changes > 0;
        listingsStatusColor = listingsAttention || !undercutAllListings ? Styling.AccentAmberSoft : Styling.AccentMintSoft;
        if (listingCount == 0)
        {
            listingsStatus = Loc.T(L.Sell.ListingsNone);
            listingsStatusColor = Styling.TextDim;
            return;
        }

        if (!undercutAllListings)
        {
            listingsStatus = Loc.T(L.Sell.MarketOff);
            return;
        }

        listingsStatus = string.Empty;
        AppendStatus(needsOk, L.Sell.ListingsNeedsOk);
        AppendStatus(changes, L.Sell.ListingsWillChange);
        AppendStatus(cheapest, L.Sell.ListingsCheapest);
        if (needsOk + changes == 0)
        {
            AppendStatus(leftAlone, L.Sell.ListingsLeftAlone);
        }
        if (listingsStatus.Length == 0)
        {
            listingsStatus = Loc.T(L.Sell.ListingsAllSet);
            listingsStatusColor = Styling.TextDim;
        }
    }

    private void AppendStatus(int count, LocPlural entry)
    {
        if (count == 0)
        {
            return;
        }

        var part = Loc.Plural(entry, count);
        listingsStatus = listingsStatus.Length == 0 ? part : string.Concat(listingsStatus, TextDraw.Separator, part);
    }

    private void BuildRetainerChips(SellPlan plan)
    {
        var retainers = plan.Retainers;
        if (retainerChips.Length != retainers.Length)
        {
            retainerChips = new RetainerChip[retainers.Length];
        }

        offRetainers = string.Empty;
        for (var index = 0; index < retainers.Length; index++)
        {
            var retainer = retainers[index];
            var count = !retainer.Enabled ? Loc.T(L.Sell.RetainersOff)
                : retainer.Visited ? string.Concat(PlanLabels.Count(retainer.Listings), "/", PlanLabels.Count(RetainerSnapshot.SlotCount))
                : PlanLabels.NoValue;
            var help = Loc.T(retainer.Enabled ? L.Sell.RetainersOnHelp : L.Sell.RetainersOffHelp, retainer.Name);
            retainerChips[index] = new RetainerChip(string.Concat(ChipIdPrefix, index.ToString(CultureInfo.InvariantCulture)), retainer.RetainerId, retainer.Name, count, help, retainer.Enabled);
            if (!retainer.Enabled)
            {
                offRetainers = offRetainers.Length == 0 ? retainer.Name : string.Concat(offRetainers, ", ", retainer.Name);
            }
        }
    }

    private void BuildRetainerChoices(SellPlan plan)
    {
        var retainers = plan.Retainers;
        if (retainerLabels.Length != retainers.Length + 1)
        {
            retainerLabels = new string[retainers.Length + 1];
            retainerIds = new ulong[retainers.Length + 1];
        }

        retainerLabels[0] = Loc.T(L.Sell.ItemsAnyRetainer);
        for (var index = 0; index < retainers.Length; index++)
        {
            retainerLabels[index + 1] = retainers[index].Name;
            retainerIds[index + 1] = retainers[index].RetainerId;
        }
    }

    private void ListingBadges(in PlanRow row, RowText text, uint approved)
    {
        var count = 0;
        if (row.Key.IsHq)
        {
            AddBadge(ref count, Loc.T(L.Rules.HighQuality), Styling.AccentAmber);
        }

        if (row.Has(PlanRowEdits.PriceSet))
        {
            AddBadge(ref count, Loc.T(L.Plan.BadgePriceSet), Styling.AccentGlow);
        }

        var decision = row.Decision;
        if (decision.Reason == DecisionReason.Approved || (decision.Reason == DecisionReason.OverStepLimit && approved > 0 && approved == decision.ProposedPrice))
        {
            AddBadge(ref count, Loc.T(L.Plan.BadgeApproved), Styling.AccentMint);
        }

        if (row.IsUndercut)
        {
            AddBadge(ref count, Loc.T(L.Plan.BadgeUndercut, Formatting.RelativeTime(row.UndercutDataUtc)), Styling.AccentRose);
        }

        text.Badges = count == 0 ? [] : badgeLabels.AsSpan(0, count).ToArray();
        text.BadgeColors = count == 0 ? [] : badgeColors.AsSpan(0, count).ToArray();
    }

    private void AddBadge(ref int count, string label, Vector4 color)
    {
        badgeLabels[count] = label;
        badgeColors[count] = color;
        count++;
    }

    private string TooltipOf(string name, string note, string detail)
    {
        tooltip.Clear().Append(name).Append('\n').Append(note);
        if (detail.Length > 0)
        {
            tooltip.Append('\n').Append(detail);
        }

        return tooltip.ToString();
    }

    private static string WhyOf(uint competitorPrice, uint floor)
    {
        var competitor = competitorPrice > 0 ? Loc.T(L.Plan.TipCompetitor, PlanLabels.Gil(competitorPrice)) : string.Empty;
        var floorText = floor > 0 ? Loc.T(L.Plan.TipFloor, PlanLabels.Gil(floor)) : string.Empty;
        if (competitor.Length == 0)
        {
            return floorText;
        }

        return floorText.Length == 0 ? competitor : string.Concat(competitor, TextDraw.Separator, floorText);
    }

    private static uint ApprovedPriceOf(SellPlan plan, in PlanRow row)
    {
        if (!row.Has(PlanRowEdits.Approved))
        {
            return 0;
        }

        return ApprovedPrice.TryFindLatest(plan.Input.PlayerApprovals.AsSpan(), row.RetainerId, row.Listing, out var approval) ? approval.Price : 0;
    }

    private static (string Note, Vector4 Color) ListingNote(in PlanRow row, bool excluded, bool manageable, bool priceSet, bool approvable, uint competitor)
    {
        if (!manageable)
        {
            return (PlanLabels.RowReason(row), Styling.TextMuted);
        }

        if (excluded)
        {
            return (Loc.T(L.Sell.NoteLeftAlone), Styling.TextDim);
        }

        if (approvable)
        {
            return (Loc.T(L.Sell.NoteWaitsOk, PlanLabels.Gil(row.Decision.ProposedPrice)), Styling.AccentAmberSoft);
        }

        if (priceSet)
        {
            return (Loc.T(L.Sell.NoteYourPrice, PlanLabels.Gil(row.Decision.Price)), Styling.AccentGlowSoft);
        }

        if (row.Decision.Action == SellAction.RepriceDown && competitor > 0)
        {
            return (Loc.T(L.Sell.NoteWillUndercut, PlanLabels.Gil(competitor), PlanLabels.Gil(row.Decision.Price)), Styling.AccentAmberSoft);
        }

        if (row.Decision.Reason == DecisionReason.AlreadyLowest)
        {
            return (Loc.T(L.Sell.NoteCheapest), Styling.AccentMintSoft);
        }

        return (PlanLabels.RowReason(row), row.Writes ? Styling.AccentAmberSoft : Styling.TextDim);
    }

    private static string ListingPlanOf(SellPlan plan, ReadOnlySpan<PlanRow> planRows, uint itemId)
    {
        for (var index = 0; index < planRows.Length; index++)
        {
            var row = planRows[index];
            if (row.Kind != RunRowKind.Lot || row.Key.ItemId != itemId || row.Has(PlanRowEdits.Excluded) || !row.IsPlaced)
            {
                continue;
            }

            plan.TryGetRetainer(row.RetainerId, out var retainer);
            return row.Decision.Action == SellAction.List
                ? Loc.T(L.Sell.NoteListOn, retainer.Name, PlanLabels.Gil(row.Decision.Price))
                : Loc.T(L.Sell.NoteListOnCheck, retainer.Name);
        }

        return Loc.T(L.Sell.NoteWillList);
    }

    private string PlacementNoteOf(ReadOnlySpan<PlanRow> planRows, uint itemId)
    {
        var note = string.Empty;
        for (var index = 0; index < planRows.Length; index++)
        {
            var row = planRows[index];
            if (row.Kind != RunRowKind.Lot || row.Key.ItemId != itemId || row.Has(PlanRowEdits.Excluded))
            {
                continue;
            }

            if (row.IsPlaced)
            {
                return string.Empty;
            }

            note = row.Placement == AllocationReason.NoFreeSlot && offRetainers.Length > 0
                ? Loc.T(L.Sell.NoteNoSlotRetainerOff, offRetainers)
                : PlanLabels.RowReason(row);
        }

        return note;
    }

    private static string QuantityText(in BagItem item)
    {
        var quantity = item.Quantity.ToString("N0", Loc.Culture);
        return item.HighQualityQuantity > 0 && item.HighQualityQuantity < item.Quantity
            ? Loc.T(L.Sell.ItemsQuantityHq, quantity, item.HighQualityQuantity.ToString("N0", Loc.Culture))
            : Loc.T(L.Sell.ItemsQuantity, quantity);
    }

    private static string SourcesText(SellSources sources)
    {
        var text = string.Empty;
        for (var index = 0; index < sourceOrder.Length; index++)
        {
            var source = sourceOrder[index];
            if ((sources & source) == 0)
            {
                continue;
            }

            var label = Loc.T(SourceLabel(source));
            text = text.Length == 0 ? label : string.Concat(text, ", ", label);
        }

        return text;
    }

    private static LocString SourceLabel(SellSources source) => source switch
    {
        SellSources.Crystals => L.Rules.SourceCrystals,
        SellSources.Saddlebag => L.Rules.SourceSaddlebag,
        SellSources.Armoury => L.Rules.SourceArmoury,
        SellSources.RetainerPages => L.Rules.SourceRetainer,
        _ => L.Rules.SourceBags,
    };
}
