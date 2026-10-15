using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text.ReadOnly;
using System.Globalization;
using System.Text;

namespace AutoSeller.Core.Driver;

internal static unsafe class AtkValueReads
{
    public const int UnreadableInteger = -1;

    private const int MaxHashedTextBytes = 4096;
    private const ulong FnvOffset = 14695981039346656037;
    private const ulong FnvPrime = 1099511628211;

    public static int Integer(in AtkValue value) => value.Type switch
    {
        AtkValueType.Int => value.Int,
        AtkValueType.UInt => (int)Math.Min(value.UInt, int.MaxValue),
        AtkValueType.Bool => value.Byte,
        _ => UnreadableInteger,
    };

    public static bool IsText(in AtkValue value)
        => (value.Type is AtkValueType.String or AtkValueType.ManagedString or AtkValueType.ConstString) && value.String.HasValue;

    public static string Text(in AtkValue value)
        => IsText(value) ? new ReadOnlySeStringSpan(value.String.Value).ExtractText() : string.Empty;

    public static long Signature(in AtkValue value)
    {
        if (IsText(value))
        {
            return (long)TextHash(value.String.Value);
        }

        return value.Type switch
        {
            AtkValueType.Int64 => value.Int64,
            AtkValueType.UInt64 => (long)value.UInt64,
            AtkValueType.Float => BitConverter.SingleToInt32Bits(value.Float),
            _ => Integer(value),
        };
    }

    public static void Append(StringBuilder builder, in AtkValue value)
    {
        if (IsText(value))
        {
            builder.Append(" '").Append(Text(value)).Append('\'');
            return;
        }

        switch (value.Type)
        {
            case AtkValueType.Int64:
                builder.Append(' ').Append(value.Int64);
                return;
            case AtkValueType.UInt64:
                builder.Append(' ').Append(value.UInt64);
                return;
            case AtkValueType.Float:
                builder.Append(' ').Append(value.Float.ToString(CultureInfo.InvariantCulture));
                return;
            default:
                builder.Append(' ').Append(Integer(value));
                return;
        }
    }

    private static ulong TextHash(byte* text)
    {
        var hash = FnvOffset;
        for (var index = 0; index < MaxHashedTextBytes && text[index] != 0; index++)
        {
            hash = (hash ^ text[index]) * FnvPrime;
        }

        return hash;
    }
}
