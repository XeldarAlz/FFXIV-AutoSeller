namespace AutoSeller.Core.Execution.Model;

internal enum ReadBackCause : byte
{
    None,
    MarketUnreadable,
    CountUnreadable,
    GilUnreadable,
    MarketMismatch,
    CountMismatch,
    GilMismatch,
}
