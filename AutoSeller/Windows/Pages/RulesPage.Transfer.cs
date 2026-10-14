using AutoSeller.Core.Localization;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Pages;

internal sealed partial class RulesPage
{
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
        pendingImportSummary = Loc.T(L.Rules.ImportSummary, document!.SellList.Count, document.CategoryRules.Count, document.NeverSell.Count, document.VendorList.Count);
    }

    private void DrawImportConfirm(Configuration configuration)
    {
        if (pendingImport is null)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        Styling.VSpace(8f);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var gap = ToolbarGap * scale;
        var replace = Loc.T(L.Rules.ImportReplace);
        var cancel = Loc.T(L.Rules.ImportCancel);
        var buttonHeight = Layout.ActionPillHeight * scale;
        var x = origin.X + width - PillButton.Width(cancel);
        ImGui.SetCursorScreenPos(new Vector2(x, origin.Y));
        if (PillButton.Draw("##asell_rules_import_cancel", cancel, Styling.AccentGlow, PillButton.Emphasis.Ghost, height: Layout.ActionPillHeight))
        {
            pendingImport = null;
        }

        x -= gap + PillButton.Width(replace);
        ImGui.SetCursorScreenPos(new Vector2(x, origin.Y));
        if (PillButton.Draw("##asell_rules_import_replace", replace, Styling.AccentRose, PillButton.Emphasis.Filled, height: Layout.ActionPillHeight)
            && pendingImport is not null)
        {
            configuration.ReplaceRules(pendingImport);
            ruleBuffers.Clear();
            minimumBuffers.Clear();
            pendingImport = null;
            ShowNotice(Loc.T(L.Rules.Imported), Styling.AccentMintSoft);
        }

        var shown = TextDraw.Truncate(pendingImportSummary, x - gap - origin.X);
        var size = TextDraw.Measure(shown);
        TextDraw.At(shown, new Vector2(origin.X, origin.Y + (buttonHeight - size.Y) * 0.5f), Styling.TextSecondary);
        if (!ReferenceEquals(shown, pendingImportSummary) && Hit.HoveringRect(origin, origin + new Vector2(x - gap - origin.X, buttonHeight)))
        {
            Tooltip.Show(pendingImportSummary);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, buttonHeight));
    }

    private static LocString ImportProblemText(RulesImportProblem problem) => problem switch
    {
        RulesImportProblem.Empty => L.Rules.ImportEmpty,
        RulesImportProblem.TooLarge => L.Rules.ImportTooLarge,
        RulesImportProblem.NewerVersion => L.Rules.ImportNewerVersion,
        _ => L.Rules.ImportMalformed,
    };
}
