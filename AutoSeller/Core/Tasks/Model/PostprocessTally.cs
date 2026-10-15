using AutoSeller.Core.Execution.Model;

namespace AutoSeller.Core.Tasks.Model;

internal sealed class PostprocessTally
{
    private ValueState carried;

    public ValueState Carried => carried;

    public ValueBook BookFor(RunCaps caps)
    {
        return ValueBook.Restore(caps, carried, default, 0, []);
    }

    public bool VendoringLeft(RunCaps caps)
    {
        return carried.VendoredValue < caps.MaxVendoredValue;
    }

    public void Keep(ValueBook values)
    {
        carried = values.Snapshot() with { PendingWriteId = Guid.Empty };
    }

    public bool Forget()
    {
        var hadTally = carried != default;
        carried = default;
        return hadTally;
    }
}
