using AutoSeller.Core;
using AutoSeller.Core.Characters;
using AutoSeller.Core.Debug;
using AutoSeller.Core.Game.Watchers;
using AutoSeller.Core.Inventory;
using AutoSeller.Core.Items;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Market;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing;
using AutoSeller.Core.Retainers;
using AutoSeller.Core.Rules;
using AutoSeller.Core.Stats;
using AutoSeller.Core.Tasks;
using AutoSeller.Windows;
using AutoSeller.Windows.Shell;
using clib;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace AutoSeller;

public sealed class Plugin : IDalamudPlugin
{
    private const string StartSubcommand = "start";
    private const string StopSubcommand = "stop";
    private const string NavmeshIpcProviderMarker = "Navmesh.IPCProvider";

    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;

    internal static Plugin Instance { get; private set; } = null!;

    internal Configuration Configuration { get; }
    internal WindowSystem WindowSystem { get; } = new("AutoSeller");
    internal RunHistory History { get; }
    internal SellController Controller { get; }
    internal ItemCatalog Items { get; }
    internal GameItems GameItems { get; }
    internal MarketServices Market { get; }
    internal RetainerReader Retainers { get; }
    internal MarketReader RetainerMarket { get; }
    internal ReferenceService References { get; }
    internal OwnSellersProvider OwnSellers { get; }
    internal CharacterStore Characters { get; }
    internal BagReader Bags { get; }
    internal BlockedScanner BlockedScanner { get; }

    private readonly DutyWatcher dutyWatcher;
    private readonly GmAlertWatcher gmAlertWatcher;
    private readonly AppWindow appWindow;
    private readonly CommandInfo primaryCommand;
    private readonly CommandInfo aliasCommand;

    // The settings file could not be read at load; said once in chat when the character is in game, with where the old
    // file was kept, null when it could not be.
    private bool configurationReset;
    private string? configurationBackupPath;

    // A step that fails undoes the ones before it, since Dalamud never disposes a plugin whose constructor threw.
    public Plugin()
    {
        Instance = this;
        var completed = new Stack<Action>();
        try
        {
            ECommonsMain.Init(PluginInterface, this);
            completed.Push(ECommonsMain.Dispose);
            CLibMain.Init(PluginInterface, this, CLibModule.Automation);
            completed.Push(CLibMain.Dispose);

            Configuration = LoadConfiguration();

            History = new RunHistory();
            Controller = new SellController();
            Items = new ItemCatalog();
            GameItems = new GameItems();
            Market = new MarketServices(Configuration);
            completed.Push(Market.Dispose);
            Retainers = new RetainerReader(Market.Gates.Allows(MarketGate.ReadRetainerMarket));
            Characters = new CharacterStore(PluginInterface.ConfigDirectory.FullName, Retainers);
            completed.Push(Characters.Dispose);
            RetainerMarket = new MarketReader(Retainers, Characters, Retainers.Available);
            References = new ReferenceService(typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "0");
            completed.Push(References.Dispose);
            OwnSellers = new OwnSellersProvider(Retainers, Characters);
            Bags = new BagReader(static _ => false);
            BlockedScanner = new BlockedScanner(Configuration, Bags, GameItems, Characters);
            completed.Push(BlockedScanner.Dispose);
            dutyWatcher = new DutyWatcher();
            completed.Push(dutyWatcher.Dispose);
            gmAlertWatcher = new GmAlertWatcher();
            completed.Push(gmAlertWatcher.Dispose);

            InitializeLocalization();
            Fonts.Initialize(PluginInterface.UiBuilder, PluginDirectory);
            completed.Push(Fonts.Dispose);
            appWindow = new AppWindow(this);
            completed.Push(appWindow.Dispose);
            WindowSystem.AddWindow(appWindow);
            completed.Push(WindowSystem.RemoveAllWindows);

            primaryCommand = new CommandInfo(OnCommand) { HelpMessage = Loc.T(L.Plugin.CommandHelp) };
            aliasCommand = new CommandInfo(OnCommand) { HelpMessage = Loc.T(L.Plugin.CommandHelpAlias) };
            CommandManager.AddHandler(AsellConstants.PrimaryCommand, primaryCommand);
            completed.Push(() => CommandManager.RemoveHandler(AsellConstants.PrimaryCommand));
            CommandManager.AddHandler(AsellConstants.AliasCommand, aliasCommand);
            completed.Push(() => CommandManager.RemoveHandler(AsellConstants.AliasCommand));

            PluginInterface.UiBuilder.Draw += OnDraw;
            completed.Push(() => PluginInterface.UiBuilder.Draw -= OnDraw);
            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
            completed.Push(() => PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi);
            PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
            completed.Push(() => PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi);

            Svc.ClientState.Login += OnLoadedInGame;
            completed.Push(() => Svc.ClientState.Login -= OnLoadedInGame);
            Svc.ClientState.Logout += OnLogout;
            completed.Push(() => Svc.ClientState.Logout -= OnLogout);
            if (Svc.ClientState.IsLoggedIn)
            {
                _ = Svc.Framework.RunOnFrameworkThread(OnLoadedInGame);
            }

            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }
        catch
        {
            Undo(completed);
            throw;
        }
    }

    private static string PluginDirectory => PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty;

    public void Dispose()
    {
        Controller.Shutdown();
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;

        PluginInterface.UiBuilder.Draw -= OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        Svc.ClientState.Login -= OnLoadedInGame;
        Svc.ClientState.Logout -= OnLogout;
        Configuration.SaveIfPending();
        BlockedScanner.Dispose();
        Characters.Dispose();
        References.Dispose();
        Market.Dispose();

        WindowSystem.RemoveAllWindows();
        appWindow.Dispose();
        Fonts.Dispose();

        CommandManager.RemoveHandler(AsellConstants.PrimaryCommand);
        CommandManager.RemoveHandler(AsellConstants.AliasCommand);

        dutyWatcher.Dispose();
        gmAlertWatcher.Dispose();

        CLibMain.Dispose();
        ECommonsMain.Dispose();
    }

    public void OnLanguageChanged()
    {
        primaryCommand.HelpMessage = Loc.T(L.Plugin.CommandHelp);
        aliasCommand.HelpMessage = Loc.T(L.Plugin.CommandHelpAlias);
    }

    public void ToggleMainUi() => appWindow.Toggle();

    public void ToggleConfigUi() => appWindow.TogglePage(AppWindow.Page.Settings);

    public void ToggleAboutUi() => appWindow.TogglePage(AppWindow.Page.About);

    public void ToggleDependenciesUi() => appWindow.TogglePage(AppWindow.Page.Plugins);

    public void ToggleHistoryUi() => appWindow.TogglePage(AppWindow.Page.History);

    public void ToggleLogUi() => appWindow.TogglePage(AppWindow.Page.Log);

    public void ToggleChangelogUi() => appWindow.TogglePage(AppWindow.Page.Changelog);

    private void OnCommand(string command, string args)
    {
        var trimmed = args.Trim();
        if (trimmed.Equals("config", StringComparison.OrdinalIgnoreCase))
        {
            ToggleConfigUi();
        }
        else if (trimmed.Equals("about", StringComparison.OrdinalIgnoreCase))
        {
            ToggleAboutUi();
        }
        else if (trimmed.Equals("deps", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("dependencies", StringComparison.OrdinalIgnoreCase))
        {
            ToggleDependenciesUi();
        }
        else if (trimmed.Equals("stats", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("history", StringComparison.OrdinalIgnoreCase))
        {
            ToggleHistoryUi();
        }
        else if (trimmed.Equals("log", StringComparison.OrdinalIgnoreCase))
        {
            ToggleLogUi();
        }
        else if (trimmed.Equals("changelog", StringComparison.OrdinalIgnoreCase))
        {
            ToggleChangelogUi();
        }
        else if (trimmed.Equals(StartSubcommand, StringComparison.OrdinalIgnoreCase))
        {
            Controller.Start();
        }
        else if (trimmed.Equals(StopSubcommand, StringComparison.OrdinalIgnoreCase))
        {
            Controller.Stop();
        }
        else if (trimmed.Equals("pause", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("resume", StringComparison.OrdinalIgnoreCase))
        {
            Controller.TogglePause();
        }
        else if (trimmed.Equals("target", StringComparison.OrdinalIgnoreCase))
        {
            TargetDumper.Dump();
        }
        else if (trimmed.Equals(MarketDebugCommands.StatusSubcommand, StringComparison.OrdinalIgnoreCase))
        {
            MarketDebugCommands.PrintStatus(this);
        }
        else if (HasSubcommand(trimmed, MarketDebugCommands.LookSubcommand))
        {
            MarketDebugCommands.Look(trimmed[MarketDebugCommands.LookSubcommand.Length..].Trim(), this);
        }
        else
        {
            ToggleMainUi();
        }
    }

    private static bool HasSubcommand(string arguments, string subcommand)
        => arguments.StartsWith(subcommand, StringComparison.OrdinalIgnoreCase)
        && (arguments.Length == subcommand.Length || char.IsWhiteSpace(arguments[subcommand.Length]));

    private void OnDraw()
    {
        WindowSystem.Draw();
        Configuration.FlushPendingSave();
    }

    // The navmesh plugin answers pathfind IPC on fire-and-forget tasks this plugin never gets a handle to. When one
    // faults, typically a query issued while the zone mesh is still building, the finalizer would rethrow it as noise.
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs)
    {
        if (eventArgs.Observed)
        {
            return;
        }

        if (!eventArgs.Exception.ToString().Contains(NavmeshIpcProviderMarker, StringComparison.Ordinal))
        {
            return;
        }

        eventArgs.SetObserved();
        RunLog.Debug($"Observed a navmesh IPC task fault: {eventArgs.Exception.GetBaseException().Message}");
    }

    private void InitializeLocalization()
    {
        var directory = Path.Combine(PluginDirectory, "Localization");
        if (string.IsNullOrEmpty(Configuration.Language))
        {
            Configuration.Language = DetectLanguage();
            Configuration.Save();
        }

        Loc.Initialize(Configuration.Language, directory);
    }

    private static string DetectLanguage()
    {
        var dalamudLanguage = PluginInterface.UiLanguage;
        if (Languages.IsKnown(dalamudLanguage))
        {
            return Languages.Resolve(dalamudLanguage).Code;
        }

        switch (Svc.ClientState.ClientLanguage)
        {
            case Dalamud.Game.ClientLanguage.German: return Languages.German.Code;
            case Dalamud.Game.ClientLanguage.French: return Languages.French.Code;
            case Dalamud.Game.ClientLanguage.Japanese: return Languages.Japanese.Code;
        }

        var osLanguage = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
        return Languages.IsKnown(osLanguage) ? Languages.Resolve(osLanguage).Code : Languages.English.Code;
    }

    private void OnLogout(int type, int code) => Controller.Stop();

    private static void Undo(Stack<Action> completed)
    {
        while (completed.Count > 0)
        {
            var step = completed.Pop();
            try
            {
                step();
            }
            catch (Exception)
            {
                // Loading has already failed; the error Dalamud reports is the one that stopped it.
            }
        }
    }

    // A settings file that cannot be read must not keep the plugin from loading: it is copied next to itself under a
    // dated name, the plugin starts from the defaults, and the chat says so once the character is in game.
    private Configuration LoadConfiguration()
    {
        Configuration? loaded = null;
        var unreadable = false;
        try
        {
            loaded = PluginInterface.GetPluginConfig() as Configuration;
            unreadable = loaded is null && PluginInterface.ConfigFile.Exists;
        }
        catch (Exception exception)
        {
            RunLog.Error(exception, "The settings file could not be read");
            unreadable = true;
        }

        if (unreadable)
        {
            configurationReset = true;
            configurationBackupPath = BackUpUnreadableConfiguration();
        }

        var configuration = loaded ?? Configuration.CreateFresh();
        if (configuration.Migrate())
        {
            configuration.Save();
        }

        return configuration;
    }

    private static string? BackUpUnreadableConfiguration()
    {
        var file = PluginInterface.ConfigFile;
        var backup = Path.Combine(file.DirectoryName ?? string.Empty, $"{Path.GetFileNameWithoutExtension(file.Name)}.unreadable-{DateTime.Now:yyyyMMdd-HHmmss}{file.Extension}");
        try
        {
            File.Copy(file.FullName, backup, overwrite: false);
            RunLog.Warning($"The unreadable settings file was kept as {backup}; starting from default settings.");
            return backup;
        }
        catch (Exception exception)
        {
            RunLog.Error(exception, $"The unreadable settings file could not be kept as {backup}; starting from default settings");
            return null;
        }
    }

    private void ShowConfigurationNotice()
    {
        if (!configurationReset)
        {
            return;
        }

        configurationReset = false;
        var notice = configurationBackupPath is { } backup ? Loc.T(L.Plugin.ConfigReset, backup) : Loc.T(L.Plugin.ConfigResetUnsaved);
        Svc.Chat.PrintError($"{AsellConstants.LogPrefix} {notice}");
    }

    private void OnLoadedInGame()
    {
        ShowConfigurationNotice();
        if (!Configuration.AutoShowOnLogin)
        {
            return;
        }

        appWindow.Show(AppWindow.Page.Sell);
    }
}
