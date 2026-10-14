namespace AutoSeller.Core.Pricing.Model;

internal readonly record struct PricingRange<T>(T Minimum, T Default, T Maximum)
    where T : struct, IComparable<T>
{
    public T Clamp(T value)
    {
        if (value.CompareTo(Minimum) < 0)
        {
            return Minimum;
        }

        return value.CompareTo(Maximum) > 0 ? Maximum : value;
    }
}
