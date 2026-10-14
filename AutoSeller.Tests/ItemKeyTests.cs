using AutoSeller.Core.Shared;

namespace AutoSeller.Tests;

public sealed class ItemKeyTests
{
    [Fact]
    public void KeysSortByItemThenNormalBeforeHighQuality()
    {
        ItemKey[] keys = [new(7, true), new(9, false), new(7, false), new(3, true)];

        Array.Sort(keys);

        Assert.Equal([new(3, true), new(7, false), new(7, true), new(9, false)], keys);
    }
}
