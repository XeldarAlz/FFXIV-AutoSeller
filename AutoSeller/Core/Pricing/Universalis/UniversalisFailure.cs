namespace AutoSeller.Core.Pricing.Universalis;

internal enum UniversalisFailure : byte
{
    None,
    NotFound,
    RateLimited,
    HttpError,
    Timeout,
    Malformed,
    ServerPaused,
}
