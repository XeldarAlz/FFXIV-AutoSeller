using System.Text.Json.Serialization;

namespace AutoSeller.Core.Execution.Model;

internal static class JournalEventKind
{
    public const string PropertyName = "Kind";
    public const string RunAuthorized = "RunAuthorized";
    public const string ListIntent = "ListIntent";
    public const string RepriceIntent = "RepriceIntent";
    public const string ReturnIntent = "ReturnIntent";
    public const string VendorIntent = "VendorIntent";
    public const string MoveIntent = "MoveIntent";
    public const string WriteConfirmed = "WriteConfirmed";
    public const string WriteRejected = "WriteRejected";
    public const string WriteUnknown = "WriteUnknown";
    public const string Reconciled = "Reconciled";
    public const string RunEnded = "RunEnded";
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = JournalEventKind.PropertyName)]
[JsonDerivedType(typeof(RunAuthorized), JournalEventKind.RunAuthorized)]
[JsonDerivedType(typeof(ListIntent), JournalEventKind.ListIntent)]
[JsonDerivedType(typeof(RepriceIntent), JournalEventKind.RepriceIntent)]
[JsonDerivedType(typeof(ReturnIntent), JournalEventKind.ReturnIntent)]
[JsonDerivedType(typeof(VendorIntent), JournalEventKind.VendorIntent)]
[JsonDerivedType(typeof(MoveIntent), JournalEventKind.MoveIntent)]
[JsonDerivedType(typeof(WriteConfirmed), JournalEventKind.WriteConfirmed)]
[JsonDerivedType(typeof(WriteRejected), JournalEventKind.WriteRejected)]
[JsonDerivedType(typeof(WriteUnknown), JournalEventKind.WriteUnknown)]
[JsonDerivedType(typeof(Reconciled), JournalEventKind.Reconciled)]
[JsonDerivedType(typeof(RunEnded), JournalEventKind.RunEnded)]
internal abstract record JournalEvent
{
    public required long Sequence { get; init; }

    public required Guid RunId { get; init; }

    public required DateTime AtUtc { get; init; }

    [JsonIgnore]
    public abstract string Kind { get; }

    public virtual bool IsWellFormed()
    {
        return Sequence > 0 && RunId != Guid.Empty;
    }
}

internal abstract record WriteEvent : JournalEvent
{
    public required Guid WriteId { get; init; }

    public override bool IsWellFormed()
    {
        return base.IsWellFormed() && WriteId != Guid.Empty;
    }
}
