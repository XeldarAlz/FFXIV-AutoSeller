namespace AutoSeller.Core.AutoRetainer.Model;

internal enum BudgetVerdict : byte { Continue, Unwind }

internal readonly record struct PostprocessBudget(TimeSpan Allowance)
{
    public const int BaseSeconds = 120;
    public const int SecondsPerWrite = 15;
    public const int CapSeconds = 600;

    public static PostprocessBudget For(int plannedWrites)
    {
        var seconds = Math.Min(BaseSeconds + ((long)Math.Max(plannedWrites, 0) * SecondsPerWrite), CapSeconds);
        return new PostprocessBudget(TimeSpan.FromSeconds(seconds));
    }

    public BudgetVerdict Judge(TimeSpan elapsed) => elapsed < Allowance ? BudgetVerdict.Continue : BudgetVerdict.Unwind;
}
