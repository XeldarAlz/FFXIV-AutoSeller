using AutoSeller.Core.Execution.Model;

namespace AutoSeller.Core.Tasks.Model;

internal sealed class PostprocessTally
{
    private ValueState carried;
    private bool carriedDryRun;

    public ValueState Carried => carried;

    public ValueBook BookFor(RunCaps caps, bool dryRun)
    {
        return ValueBook.Restore(caps, CarriedFor(dryRun), default, 0, []);
    }

    public bool VendoringLeft(RunCaps caps, bool dryRun)
    {
        return CarriedFor(dryRun).VendoredValue < caps.MaxVendoredValue;
    }

    public void Keep(ValueBook values, bool dryRun)
    {
        carried = values.Snapshot() with { PendingWriteId = Guid.Empty };
        carriedDryRun = dryRun;
    }

    public bool Forget()
    {
        var hadTally = carried != default;
        carried = default;
        return hadTally;
    }

    private ValueState CarriedFor(bool dryRun)
    {
        return dryRun == carriedDryRun ? carried : default;
    }
}
