using AutoSeller.Core.Localization;
using AutoSeller.Core.Stats;
using AutoSeller.Windows.Components;
using Dalamud.Interface;

namespace AutoSeller.Windows.Pages;

internal sealed class HistoryPage
{
    private const float HeaderGap = 14f;

    public void Draw(RunHistory history)
    {
        PageHeader.Draw(Loc.T(L.History.Title), Loc.T(L.History.Empty));
        Styling.VSpace(HeaderGap);
        if (history.Records.Count == 0)
        {
            EmptyState.Draw(FontAwesomeIcon.History, Loc.T(L.History.NoRuns));
        }
    }
}
