using System.Globalization;
using System.Text;

namespace AutoSeller.Core.Sales.Model;

internal interface ISaleItemNames
{
    bool TryGetName(uint itemId, out string name);
}

internal static class SalesCsv
{
    public const string Header = "sold_at_utc,item_id,item_name,quality,quantity,unit_price,net_gil,listed_at_utc,source,confidence";

    private const char Separator = ',';
    private const char Quote = '"';
    private const char NewLine = '\n';
    private const string CharactersNeedingQuotes = ",\"\r\n";
    private const string HighQuality = "HQ";
    private const string NormalQuality = "NQ";
    private const int CharactersPerRow = 112;

    public static string Write(ReadOnlySpan<SaleRecord> sales, ISaleItemNames names)
    {
        var builder = new StringBuilder(Header.Length + 1 + sales.Length * CharactersPerRow);
        builder.Append(Header).Append(NewLine);
        for (var index = 0; index < sales.Length; index++)
        {
            AppendRow(builder, sales[index], names);
        }

        return builder.ToString();
    }

    private static void AppendRow(StringBuilder builder, in SaleRecord sale, ISaleItemNames names)
    {
        var invariant = CultureInfo.InvariantCulture;
        AppendTimestamp(builder, sale.SoldAtUtc).Append(Separator);
        builder.Append(invariant, $"{sale.ItemId}").Append(Separator);
        AppendField(builder, names.TryGetName(sale.ItemId, out var name) ? name : string.Empty);
        builder.Append(Separator).Append(sale.IsHq ? HighQuality : NormalQuality).Append(Separator);
        builder.Append(invariant, $"{sale.Quantity}").Append(Separator);
        if (sale.HasUnitPrice)
        {
            builder.Append(invariant, $"{sale.UnitPrice}");
        }

        builder.Append(Separator);
        builder.Append(invariant, $"{sale.NetGil}").Append(Separator);
        if (sale.HasListingTime)
        {
            AppendTimestamp(builder, sale.ListedAtUtc);
        }

        builder.Append(Separator).Append(SourceText(sale.Source));
        builder.Append(Separator).Append(ConfidenceText(sale.Confidence)).Append(NewLine);
    }

    private static StringBuilder AppendTimestamp(StringBuilder builder, DateTime atUtc)
    {
        return builder.Append(CultureInfo.InvariantCulture, $"{atUtc:yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'}");
    }

    private static void AppendField(StringBuilder builder, string value)
    {
        if (value.AsSpan().IndexOfAny(CharactersNeedingQuotes) < 0)
        {
            builder.Append(value);
            return;
        }

        builder.Append(Quote);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character == Quote)
            {
                builder.Append(Quote);
            }

            builder.Append(character);
        }

        builder.Append(Quote);
    }

    private static string SourceText(SaleSource source)
    {
        return source switch
        {
            SaleSource.Notice => "notice",
            SaleSource.History => "history",
            _ => "diff",
        };
    }

    private static string ConfidenceText(SaleConfidence confidence)
    {
        return confidence == SaleConfidence.Confirmed ? "confirmed" : "inferred";
    }
}
