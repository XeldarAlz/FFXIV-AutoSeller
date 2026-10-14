using System.Collections.Immutable;
using System.IO;
using System.Text;
using AutoSeller.Core.Pricing.Universalis;

namespace AutoSeller.Tests.Universalis;

internal static class UniversalisFixtures
{
    public const uint Phoenix = 56;
    public const uint Odin = 66;
    public const uint Shiva = 67;
    public const uint Louisoix = 83;
    public const uint Raiden = 403;

    public const uint BusyItem = 5057;
    public const uint QuietItem = 36112;
    public const uint FireShard = 2;
    public const uint GilItem = 1;
    public const uint UnknownItem = 99_999_999;
    public const uint UnaskedItem = 3;

    public static readonly DateTime RecordedAt = new(2026, 10, 14, 11, 0, 0, DateTimeKind.Utc);

    public static readonly UniversalisScope PhoenixScope = UniversalisScope.ForWorld(Phoenix, "Phoenix");
    public static readonly UniversalisScope LightScope = UniversalisScope.ForDataCenter("Light");

    public static byte[] Read(string name)
    {
        return File.ReadAllBytes(Path.Combine(RepositoryFiles.Root, "AutoSeller.Tests", "Fixtures", "Universalis", name));
    }

    public static byte[] Utf8(string json)
    {
        return Encoding.UTF8.GetBytes(json);
    }

    public static DateTime FromUnixMilliseconds(long milliseconds)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
    }

    public static ReferenceChunk Chunk(UniversalisScope scope, params uint[] itemIds)
    {
        return new ReferenceChunk(scope, [.. itemIds]);
    }

    public static ReferenceChunkResult Translate(string fixture, UniversalisScope scope, params uint[] itemIds)
    {
        return UniversalisTranslator.TranslateReferences(Read(fixture), Chunk(scope, itemIds));
    }

    public static UniversalisReference Item(ReferenceChunkResult result, uint itemId)
    {
        var items = result.Items;
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].ItemId == itemId)
            {
                return items[index];
            }
        }

        throw new InvalidOperationException($"Item {itemId} is not in the result.");
    }

    public static ImmutableArray<ReferenceChunkResult> Results(params ReferenceChunkResult[] results)
    {
        return [.. results];
    }
}
