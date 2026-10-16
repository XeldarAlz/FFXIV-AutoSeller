using AutoSeller.Core.Localization;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Pages;

internal sealed partial class RulesPage
{
    private const float ConfirmTint = 0.10f;

    private RulesDocument? pendingImport;
    private string pendingImportSummary = string.Empty;

    private void Export(Configuration configuration)
    {
        var text = RulesTransfer.Export(configuration.SellList, configuration.CategoryRules, configuration.NeverSell, configuration.VendorList, configuration.JunkThreshold);
        ImGui.SetClipboardText(text);
        ShowNotice(Loc.T(L.Rules.ExportCopied), Styling.AccentMintSoft);
    }

    private void ReadImport()
    {
        var problem = RulesTransfer.TryImport(ImGui.GetClipboardText() ?? string.Empty, out var document);
        if (problem != RulesImportProblem.None)
        {
            pendingImport = null;
            ShowNotice(Loc.T(ImportProblemText(problem)), Styling.AccentRoseSoft);
            return;
        }

        pendingImport = document;
        pendingImportSummary = Loc.T(L.Rules.ImportSummary,
            Loc.Plural(L.Rules.ImportSellRules, document!.SellList.Count),
            Loc.Plural(L.Rules.ImportCategoryRules, document.CategoryRules.Count),
            Loc.Plural(L.Rules.ImportNeverSell, document.NeverSell.Count),
            Loc.Plural(L.Rules.ImportVendorItems, document.VendorList.Count));
    }

    private void DrawImportConfirm(Configuration configuration, in BoardBand band)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        Paint.Fill(drawList, band.Min, band.Max, Styling.WithAlpha(Styling.AccentRose, ConfirmTint), 0f);
        var gap = ToolbarGap * scale;
        var replace = Loc.T(L.Rules.ImportReplace);
        var cancel = Loc.T(L.Rules.ImportCancel);
        var buttonY = band.MidY - Layout.ActionPillHeight * scale * 0.5f;
        var x = band.Right - PillButton.Width(cancel);
        ImGui.SetCursorScreenPos(new Vector2(x, buttonY));
        if (PillButton.Draw("##asell_rules_import_cancel", cancel, Styling.AccentGlow, PillButton.Emphasis.Ghost, height: Layout.ActionPillHeight))
        {
            pendingImport = null;
        }

        x -= gap + PillButton.Width(replace);
        ImGui.SetCursorScreenPos(new Vector2(x, buttonY));
        if (PillButton.Draw("##asell_rules_import_replace", replace, Styling.AccentRose, PillButton.Emphasis.Filled, height: Layout.ActionPillHeight)
            && pendingImport is not null)
        {
            configuration.ReplaceRules(pendingImport);
            ruleBuffers.Clear();
            minimumBuffers.Clear();
            pendingImport = null;
            ShowNotice(Loc.T(L.Rules.Imported), Styling.AccentMintSoft);
        }

        var right = x - gap;
        var shown = TextDraw.Truncate(pendingImportSummary, MathF.Max(0f, right - band.Left));
        TextDraw.At(shown, new Vector2(band.Left, band.MidY - ImGui.GetTextLineHeight() * 0.5f), Styling.TextStrong);
        if (!ReferenceEquals(shown, pendingImportSummary) && Hit.HoveringRect(band.Min, new Vector2(right, band.Max.Y)))
        {
            Tooltip.Show(pendingImportSummary);
        }
    }

    private static LocString ImportProblemText(RulesImportProblem problem) => problem switch
    {
        RulesImportProblem.Empty => L.Rules.ImportEmpty,
        RulesImportProblem.TooLarge => L.Rules.ImportTooLarge,
        RulesImportProblem.NewerVersion => L.Rules.ImportNewerVersion,
        _ => L.Rules.ImportMalformed,
    };
}
