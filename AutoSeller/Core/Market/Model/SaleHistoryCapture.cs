namespace AutoSeller.Core.Market.Model;

internal sealed class SaleHistoryCapture
{
    public const int Capacity = 20;

    private readonly MarketSale[] sales = new MarketSale[Capacity];
    private int count;

    public uint ItemId { get; private set; }

    public int Generation { get; private set; }

    public ReadOnlySpan<MarketSale> Sales => sales.AsSpan(0, count);

    public static bool BelongsToSearch(uint historyItemId, uint searchItemId, ListingRequestStatus searchStatus)
    {
        return historyItemId == searchItemId && searchStatus is ListingRequestStatus.Waiting or ListingRequestStatus.Complete;
    }

    public void Begin(uint itemId, int generation)
    {
        ItemId = itemId;
        Generation = generation;
        count = 0;
    }

    public void Add(in MarketSale sale)
    {
        if (count == Capacity)
        {
            return;
        }

        sales[count] = sale;
        count++;
    }

    public ReadOnlySpan<MarketSale> For(uint itemId, int generation)
    {
        return ItemId == itemId && Generation == generation ? Sales : [];
    }

    public bool AttachesLateTo(CompetitorSnapshot snapshot, int currentGeneration)
    {
        return count > 0
            && snapshot.History.IsEmpty
            && snapshot.ItemId == ItemId
            && snapshot.Generation == Generation
            && currentGeneration == Generation;
    }

    public void Clear()
    {
        ItemId = 0;
        Generation = 0;
        count = 0;
    }
}
