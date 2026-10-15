using AutoSeller.Core.Driver;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Localization;
using AutoSeller.Windows.Sections;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace AutoSeller.Windows.Overlays;

internal sealed class AssistCard(Plugin plugin, AssistHost host) : AddonOverlay("Auto Seller Assist###AutoSellerAssistCard", RetainerClicks.SellWindowAddon)
{
    private const float CardWidth = 300f;
    private const float PadX = 14f;
    private const float PadY = 12f;
    private const float LineGap = 4f;
    private const float Gap = 8f;
    private const int Lines = 6;

    private static readonly LocString[] skipKeyNames = [L.Assist.KeyShift, L.Assist.KeyControl, L.Assist.KeyAlt];

    private readonly string[] lines = new string[Lines];
    private readonly Vector4[] colors = new Vector4[Lines];
    private long builtRevision = -1;
    private LanguageInfo? builtLanguage;
    private AssistSkipKey builtSkipKey;
    private int lineCount;

    protected override bool Shows()
    {
        return host.View.Status != AssistStatus.Idle && !plugin.Controller.HoldsRetainerWindows;
    }

    protected override Vector2 Estimate()
    {
        return new Vector2(CardWidth * Scale, CardWidth * 0.5f * Scale);
    }

    protected override ScreenRect Place(in ScreenRect window, Vector2 size, in ScreenRect screen)
    {
        return OverlayPlacement.Beside(window, size.X, size.Y, Gap * Scale, screen);
    }

    protected override void DrawOverlay()
    {
        var view = host.View;
        Build(view);
        var scale = Scale;
        var width = CardWidth * scale;
        var padX = PadX * scale;
        var padY = PadY * scale;
        var lineGap = LineGap * scale;
        var wrapWidth = width - padX * 2f;
        var heights = (Span<float>)stackalloc float[Lines];
        var height = padY * 2f + TextDraw.SmallCapsSize(lines[0]).Y;
        for (var index = 1; index < lineCount; index++)
        {
            heights[index] = TextDraw.MeasureWrapped(lines[index], wrapWidth).Y;
            height += lineGap + heights[index];
        }

        var origin = ImGui.GetCursorScreenPos();
        var accent = AccentOf(view);
        var drawList = ImGui.GetWindowDrawList();
        Paint.Surface(drawList, origin, origin + new Vector2(width, height), Styling.CardRounding * scale, Styling.Tint(Styling.WindowBg, accent, 0.08f), Styling.WithAlpha(accent, 0.5f));
        var y = origin.Y + padY;
        TextDraw.SmallCaps(lines[0], new Vector2(origin.X + padX, y), accent);
        y += TextDraw.SmallCapsSize(lines[0]).Y;
        for (var index = 1; index < lineCount; index++)
        {
            y += lineGap;
            TextDraw.Wrapped(lines[index], new Vector2(origin.X + padX, y), wrapWidth, colors[index]);
            y += heights[index];
        }

        ImGui.Dummy(new Vector2(width, height));
    }

    private static Vector4 AccentOf(AssistView view)
    {
        return view.Status switch
        {
            AssistStatus.Decided when view.Suggestion.Outcome == AssistOutcome.Typed => Styling.AccentMint,
            AssistStatus.Decided when view.Suggestion.Outcome == AssistOutcome.BelowFloor => Styling.AccentAmber,
            AssistStatus.ItemUnknown or AssistStatus.SearchFailed => Styling.AccentAmber,
            _ => Styling.AccentGlow,
        };
    }

    private void Build(AssistView view)
    {
        var skipKey = plugin.Configuration.AssistSkipKey;
        if (view.Revision == builtRevision && ReferenceEquals(builtLanguage, Loc.Current) && skipKey == builtSkipKey)
        {
            return;
        }

        builtRevision = view.Revision;
        builtLanguage = Loc.Current;
        builtSkipKey = skipKey;
        lineCount = 0;
        Add(string.Concat(Loc.T(L.Assist.CardTitle), TextDraw.Separator, view.ItemName), Styling.TextDim);
        switch (view.Status)
        {
            case AssistStatus.Comparing:
                Add(Loc.T(L.Assist.CardComparing), Styling.TextStrong);
                break;
            case AssistStatus.ItemUnknown:
                Add(Loc.T(L.Assist.CardItemUnknown), Styling.TextStrong);
                break;
            case AssistStatus.SearchFailed:
                Add(Loc.T(L.Assist.CardSearchFailed), Styling.TextStrong);
                break;
            default:
                AddDecision(view);
                break;
        }

        Add(Loc.T(L.Assist.CardSkipHint, Loc.T(skipKeyNames[(int)skipKey])), Styling.TextMuted);
    }

    private void AddDecision(AssistView view)
    {
        var suggestion = view.Suggestion;
        var price = PlanLabels.Gil(suggestion.Price);
        Add(suggestion.Outcome switch
        {
            AssistOutcome.Typed => Loc.T(L.Assist.CardTyped, price),
            AssistOutcome.AlreadyShown => Loc.T(L.Assist.CardAlreadyShown, price),
            AssistOutcome.DryRun => Loc.T(L.Assist.CardDryRun, price),
            AssistOutcome.BelowFloor => Loc.T(L.Assist.CardBelowFloor, price),
            _ => suggestion.Price > 0 ? string.Concat(Loc.T(L.Assist.CardNotTyped), TextDraw.Separator, Loc.T(L.Plan.TipProposed, price)) : Loc.T(L.Assist.CardNotTyped),
        }, Styling.TextStrong);
        var reason = PlanLabels.Reason(view.Decision.Reason, view.CooldownLeft);
        if (reason.Length > 0)
        {
            Add(reason, Styling.TextSecondary);
        }

        var evidence = view.Decision.Evidence;
        var facts = string.Concat(
            Loc.T(L.Plan.TipFloor, PlanLabels.Gil(suggestion.Floor)),
            TextDraw.Separator,
            evidence.ReferencePrice > 0 ? Loc.T(L.Plan.TipReference, PlanLabels.Gil(evidence.ReferencePrice), PlanLabels.ReferenceFrom(evidence.ReferenceSource)) : Loc.T(L.Assist.CardNoReference));
        Add(facts, Styling.TextDim);
        if (evidence.CompetitorPrice > 0)
        {
            Add(Loc.T(L.Plan.TipCompetitor, PlanLabels.Gil(evidence.CompetitorPrice)), Styling.TextDim);
        }
    }

    private void Add(string text, Vector4 color)
    {
        if (lineCount >= Lines)
        {
            return;
        }

        lines[lineCount] = text;
        colors[lineCount] = color;
        lineCount++;
    }
}
