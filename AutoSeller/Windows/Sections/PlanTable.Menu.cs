using System.Globalization;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal sealed partial class PlanTable
{
    private const string MenuId = "##asell_plan_menu";
    private const string PriceFieldId = "##asell_plan_menu_price";
    private const string ApplyId = "##asell_plan_menu_apply";
    private const string MoveIdPrefix = "##asell_plan_menu_move_";
    private const float PriceFieldWidth = 120f;

    private readonly GilField.Buffer priceBuffer = new();
    private PlanRowKey menuKey;
    private string[] moveLabels = [];
    private ulong[] moveTargets = [];
    private string menuFloor = string.Empty;
    private string menuRefusal = string.Empty;
    private long menuPrice;

    private void OpenMenu(Plugin plugin, SellPlan plan, int index)
    {
        if (!plugin.Plan.CanEdit)
        {
            return;
        }

        var row = plan.Rows[index];
        menuKey = row.Key;
        menuRefusal = string.Empty;
        menuPrice = row.Has(PlanRowEdits.PriceSet) ? row.Decision.Price : 0;
        priceBuffer.Reset();
        menuFloor = Loc.T(L.Plan.MenuFloor, PlanLabels.Gil(row.Floor));
        BuildMoveTargets(plan, row);
        ImGui.OpenPopup(MenuId);
    }

    private void BuildMoveTargets(SellPlan plan, in PlanRow row)
    {
        if (!row.CanMove)
        {
            moveLabels = [];
            moveTargets = [];
            return;
        }

        var retainers = plan.Retainers;
        var labels = new List<string>(retainers.Length);
        var targets = new List<ulong>(retainers.Length);
        for (var index = 0; index < retainers.Length; index++)
        {
            var retainer = retainers[index];
            if (!retainer.Enabled || retainer.RetainerId == row.RetainerId)
            {
                continue;
            }

            labels.Add(string.Concat(Loc.T(L.Plan.MenuMoveTo, retainer.Name), MoveIdPrefix, index.ToString(CultureInfo.InvariantCulture)));
            targets.Add(retainer.RetainerId);
        }

        moveLabels = [.. labels];
        moveTargets = [.. targets];
    }

    private void DrawMenu(Plugin plugin, SellPlan plan)
    {
        using var menu = ContextMenu.Begin(MenuId);
        if (!menu.Open)
        {
            return;
        }

        if (!plan.TryFind(menuKey, out var row) || !plugin.Plan.CanEdit)
        {
            ImGui.CloseCurrentPopup();
            return;
        }

        var builder = plugin.Plan;
        var excluded = row.Has(PlanRowEdits.Excluded);
        if (ImGui.Selectable(Loc.T(excluded ? L.Plan.MenuInclude : L.Plan.MenuExclude)))
        {
            builder.SetExcluded(row, !excluded);
        }

        if (moveLabels.Length > 0 || row.Has(PlanRowEdits.Moved))
        {
            ImGui.Separator();
            for (var index = 0; index < moveLabels.Length; index++)
            {
                if (ImGui.Selectable(moveLabels[index]))
                {
                    Refused(builder.MoveTo(row, moveTargets[index]), row);
                }
            }

            if (row.Has(PlanRowEdits.Moved) && ImGui.Selectable(Loc.T(L.Plan.MenuPlaceByValue)))
            {
                builder.ClearMove(row);
            }
        }

        ImGui.Separator();
        DrawPriceEditor(builder, row);
        if (row.Has(PlanRowEdits.PriceSet) && ImGui.Selectable(Loc.T(L.Plan.MenuClearPrice)))
        {
            builder.ClearPrice(row);
        }

        if (row.Has(PlanRowEdits.Approved) && ImGui.Selectable(Loc.T(L.Plan.MenuWithdraw)))
        {
            builder.Withdraw(row);
        }
    }

    private void DrawPriceEditor(Core.Planning.PlanBuilder builder, in PlanRow row)
    {
        ImGui.TextUnformatted(Loc.T(L.Plan.MenuSetPrice));
        if (GilField.Draw(PriceFieldId, priceBuffer, menuPrice, MarketLimits.MaximumUnitPrice, Loc.T(L.Plan.MenuPriceHint), PriceFieldWidth, out var typed))
        {
            menuPrice = typed;
            menuRefusal = string.Empty;
        }

        ImGui.SameLine();
        if (PillButton.Draw(ApplyId, Loc.T(L.Plan.MenuApply), Styling.AccentGlow, PillButton.Emphasis.Tinted, null, menuPrice > 0))
        {
            var refusal = builder.SetPrice(row, (uint)Math.Clamp(menuPrice, 0, MarketLimits.MaximumUnitPrice));
            if (refusal == PlanEditRefusal.None)
            {
                ImGui.CloseCurrentPopup();
            }

            Refused(refusal, row);
        }

        using (Fonts.PushCaption())
        {
            var color = menuRefusal.Length > 0 ? Styling.AccentRoseSoft : Styling.TextMuted;
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextUnformatted(menuRefusal.Length > 0 ? menuRefusal : menuFloor);
            ImGui.PopStyleColor();
        }
    }

    private void Refused(PlanEditRefusal refusal, in PlanRow row)
    {
        menuRefusal = refusal switch
        {
            PlanEditRefusal.None => string.Empty,
            PlanEditRefusal.PriceBelowFloor => Loc.T(L.Plan.EditBelowFloor, PlanLabels.Gil(row.Floor)),
            PlanEditRefusal.PriceOutOfRange => Loc.T(L.Plan.EditOutOfRange),
            PlanEditRefusal.RunInProgress => Loc.T(L.Plan.EditLocked),
            _ => Loc.T(L.Plan.EditNotMovable),
        };
    }
}
