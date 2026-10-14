using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;
using Dalamud.Interface;

namespace AutoSeller.Windows.Pages;

internal sealed class SellPage
{
    public void Draw()
    {
        PageHeader.Draw(Loc.T(L.Sell.Title), Loc.T(L.Sell.Subtitle));
        EmptyState.Draw(FontAwesomeIcon.ClipboardList, Loc.T(L.Sell.Empty));
    }
}
