using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text.ReadOnly;
using System.Text;

namespace AutoSeller.Core.Driver;

internal static unsafe class AtkValueReads
{
    public const int UnreadableInteger = -1;

    private const int MaxDescribedValues = 64;

    public static bool IsInteger(in AtkValue value) => value.Type is AtkValueType.Int or AtkValueType.UInt;

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

    public static string Describe(AtkUnitBase* unit)
    {
        if (unit->AtkValues == null)
        {
            return "no values";
        }

        var valueCount = Math.Min((int)unit->AtkValuesCount, MaxDescribedValues);
        var builder = new StringBuilder();
        builder.Append($"{unit->AtkValuesCount} values");
        for (var valueIndex = 0; valueIndex < valueCount; valueIndex++)
        {
            ref readonly var value = ref unit->AtkValues[valueIndex];
            builder.Append($"; [{valueIndex}] {value.Type}");
            if (IsText(value))
            {
                builder.Append($" '{Text(value)}'");
            }
            else if (value.Type is AtkValueType.Int or AtkValueType.UInt or AtkValueType.Bool)
            {
                builder.Append($" {Integer(value)}");
            }
        }

        return builder.ToString();
    }
}
