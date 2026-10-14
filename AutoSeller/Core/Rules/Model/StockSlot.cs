namespace AutoSeller.Core.Rules.Model;

internal readonly record struct StockSlot(SlotFacts Slot, ItemFacts Item, uint ReferenceUnitPrice);
