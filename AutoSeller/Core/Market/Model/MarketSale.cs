namespace AutoSeller.Core.Market.Model;

internal readonly record struct MarketSale(uint UnitPrice, uint Quantity, bool IsHq, bool OnMannequin, DateTime SoldAtUtc);
