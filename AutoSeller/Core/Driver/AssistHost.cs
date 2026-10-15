using System.Threading;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Tasks;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Keys;
using ECommons.DalamudServices;
using ClibServices = clib.Services.Svc;

namespace AutoSeller.Core.Driver;

internal sealed class AssistHost : IDisposable
{
    private readonly Plugin plugin;
    private AssistView view = AssistView.Idle;
    private AssistTask? task;
    private long revision;

    public AssistHost(Plugin plugin)
    {
        this.plugin = plugin;
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, RetainerClicks.SellWindowAddon, OnSellWindowSetup);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, RetainerClicks.SellWindowAddon, OnSellWindowClosing);
    }

    public AssistView View => Volatile.Read(ref view);

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, RetainerClicks.SellWindowAddon, OnSellWindowSetup);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, RetainerClicks.SellWindowAddon, OnSellWindowClosing);
        var automation = ClibServices.Automation;
        if (ReferenceEquals(automation.CurrentTask, task))
        {
            automation.Stop();
        }
    }

    public void Show(AssistStatus status, string itemName, in AssistSuggestion suggestion, in Decision decision, TimeSpan cooldownLeft)
    {
        Volatile.Write(ref view, new AssistView(Interlocked.Increment(ref revision), status, itemName, suggestion, decision, cooldownLeft));
    }

    private void OnSellWindowSetup(AddonEvent type, AddonArgs args)
    {
        Volatile.Write(ref view, AssistView.Idle);
        var configuration = plugin.Configuration;
        var automation = ClibServices.Automation;
        var block = AssistGate.Check(new AssistFacts(
            configuration.AssistEnabled,
            plugin.Controller.HoldsRetainerWindows,
            automation.Running && !ReferenceEquals(automation.CurrentTask, task),
            SkipKeyHeld(configuration.AssistSkipKey),
            plugin.Market.Gates.Allows(MarketGate.ReadListings)));
        if (block != AssistBlock.None)
        {
            if (block is not (AssistBlock.AssistOff or AssistBlock.RunOwnsWindows))
            {
                RunLog.Info($"Assist: a sell window opened, and Assist leaves it alone ({block}).");
            }

            return;
        }

        RunLog.Info("Assist: a sell window was opened by hand; its prices are compared.");
        if (TaskStarter.TryStart(() => task = new AssistTask(plugin, this)))
        {
            plugin.Marketbuddy.Hold();
        }
    }

    private void OnSellWindowClosing(AddonEvent type, AddonArgs args)
    {
        Volatile.Write(ref view, AssistView.Idle);
    }

    private static bool SkipKeyHeld(AssistSkipKey key)
    {
        return Svc.KeyState[key switch
        {
            AssistSkipKey.Control => VirtualKey.CONTROL,
            AssistSkipKey.Alt => VirtualKey.MENU,
            _ => VirtualKey.SHIFT,
        }];
    }
}
