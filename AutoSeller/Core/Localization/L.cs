namespace AutoSeller.Core.Localization;

internal static partial class L
{
    internal static class Common
    {
        public static readonly LocString Close = new("common.close", "Close");
        public static readonly LocString Pause = new("common.pause", "Pause");
        public static readonly LocString Resume = new("common.resume", "Resume");
        public static readonly LocString StopRun = new("common.stopRun", "Stop the run");
        public static readonly LocString DragAdjustHint = new("common.dragAdjustHint", "Drag to adjust · Ctrl+click to type");
        public static readonly LocString NoMatches = new("common.noMatches", "Nothing matches “{0}”.");
        public static readonly LocString Gil = new("common.gil", "{0} gil");
        public static readonly LocString DurationMinutes = new("common.duration.minutes", "{0} min");
        public static readonly LocString DurationMinutesSeconds = new("common.duration.minutesSeconds", "{0} min {1} s");
        public static readonly LocString DurationHours = new("common.duration.hours", "{0} h {1} min");
        public static readonly LocString DurationDays = new("common.duration.days", "{0} d {1} h");
    }

    internal static class Shell
    {
        public static readonly LocString NavSell = new("shell.nav.sell", "Sell");
        public static readonly LocString NavListings = new("shell.nav.listings", "Listings");
        public static readonly LocString NavSales = new("shell.nav.sales", "Sales");
        public static readonly LocString NavRules = new("shell.nav.rules", "Rules");
        public static readonly LocString NavSettings = new("shell.nav.settings", "Settings");
        public static readonly LocString NavHistory = new("shell.nav.history", "History");
        public static readonly LocString NavPlugins = new("shell.nav.plugins", "Plugins");
        public static readonly LocString NavLog = new("shell.nav.log", "Console");
        public static readonly LocString NavChangelog = new("shell.nav.changelog", "Changelog");
        public static readonly LocString NavAbout = new("shell.nav.about", "About");
        public static readonly LocString StatusRunning = new("shell.status.running", "Running");
        public static readonly LocString StatusPaused = new("shell.status.paused", "Paused");
        public static readonly LocString StatusReady = new("shell.status.ready", "Ready");
        public static readonly LocString StatusBlocked = new("shell.status.blocked", "Not ready");
        public static readonly LocString StatusSetupNeeded = new("shell.status.setupNeeded", "Setup needed");
        public static readonly LocString StatusIdle = new("shell.status.idle", "Idle");
        public static readonly LocString Minimize = new("shell.minimize", "Minimize to the title strip");
        public static readonly LocString Restore = new("shell.restore", "Restore the window");
        public static readonly LocString ResumeBlocked = new("shell.resumeBlocked", "Resumes automatically once you leave the duty");
    }

    internal static class Sell
    {
        public static readonly LocString Title = new("sell.title", "Sell");
        public static readonly LocString Subtitle = new("sell.subtitle", "Your retainers' listings and the plan for the next run.");
        public static readonly LocString Empty = new("sell.empty", "Nothing planned yet. Your retainers' listings and the next run's plan will show up here.");
    }

    internal static class Dock
    {
        public static readonly LocString Start = new("dock.start", "START");
        public static readonly LocString StartSub = new("dock.startSub", "reprice and list");
        public static readonly LocString Stop = new("dock.stop", "STOP");
        public static readonly LocString PauseCaps = new("dock.pause", "PAUSE");
        public static readonly LocString ResumeCaps = new("dock.resume", "RESUME");
        public static readonly LocString InContent = new("dock.inContent", "in content");
        public static readonly LocString ReasonInstall = new("dock.reason.install", "install the required plugins");
        public static readonly LocString ReasonLoggedOut = new("dock.reason.loggedOut", "log in first");
        public static readonly LocString ReasonJournal = new("dock.reason.journal", "answer the card on the Sell page first");
        public static readonly LocString StateRunning = new("dock.state.running", "running");
        public static readonly LocString StatePaused = new("dock.state.paused", "paused");
    }

    internal static class Run
    {
        public static readonly LocString PhasePreparing = new("run.phase.preparing", "Getting ready");
        public static readonly LocString PhaseStandingBy = new("run.phase.standingBy", "Standing by");
        public static readonly LocString PhasePaused = new("run.phase.paused", "Paused");
        public static readonly LocString ChatTaskFailed = new("run.chat.taskFailed", "A task could not be started. The log has the details.");
        public static readonly LocString PhaseTravelling = new("run.phase.travelling", "Going to the bell");
        public static readonly LocString PhaseSelling = new("run.phase.selling", "At the retainers");
        public static readonly LocString PhaseFinishing = new("run.phase.finishing", "Finishing");
        public static readonly LocString PhaseStopping = new("run.phase.stopping", "Stopping");
        public static readonly LocString StatusPreparing = new("run.status.preparing", "Reading your bags and the reference prices");
        public static readonly LocString StatusSettling = new("run.status.settling", "Waiting for the stopped run to close its windows");
        public static readonly LocString StatusGoingToBell = new("run.status.goingToBell", "Going to a summoning bell");
        public static readonly LocString StatusOpeningBell = new("run.status.openingBell", "Opening the retainer list");
        public static readonly LocString StatusRetainer = new("run.status.retainer", "Retainer {0} of {1}: {2}");
        public static readonly LocString StatusReadingMarket = new("run.status.readingMarket", "{0}: reading the listings");
        public static readonly LocString StatusListing = new("run.status.listing", "{0}: checking {1} ({2} of {3})");
        public static readonly LocString StatusLot = new("run.status.lot", "{0}: pricing {1} for a new listing ({2} of {3})");
        public static readonly LocString StatusSaleHistory = new("run.status.saleHistory", "{0}: reading the sale history");
        public static readonly LocString StatusReturning = new("run.status.returning", "{0}: returning {1} to the retainer");
        public static readonly LocString StatusVendoring = new("run.status.vendoring", "{0}: having the retainer sell {1} to a vendor");
        public static readonly LocPlural StatusSaddlebag = new("run.status.saddlebag", "Moving {0} stack from the saddlebag to your bags", "Moving {0} stacks from the saddlebag to your bags");
        public static readonly LocString StatusRetainerPass = new("run.status.retainerPass", "{0}: selling from the retainer's own inventory");
        public static readonly LocString StatusLeaving = new("run.status.leaving", "{0}: closing the sell list");
        public static readonly LocString StatusClosing = new("run.status.closing", "Closing the retainer list");
        public static readonly LocString StatusPostprocess = new("run.status.postprocess", "{0}: selling for AutoRetainer");
        public static readonly LocString StatusHandingBack = new("run.status.handingBack", "Handing {0} back to AutoRetainer");
        public static readonly LocString StatusPausing = new("run.status.pausing", "Pausing after this item");
        public static readonly LocString StatusPausedInContent = new("run.status.pausedInContent", "Paused while you are in content");
        public static readonly LocString StatusPausedInterrupted = new("run.status.pausedInterrupted", "Paused: combat or a duty pop interrupted the run; it carries on once that is over");
        public static readonly LocString StatusStopping = new("run.status.stopping", "Stopping and closing the sell windows");
        public static readonly LocString StatusDone = new("run.status.done", "Dry run done: {0} to reprice, {1} to list, {2} kept, {3} skipped");
        public static readonly LocString StatusDoneLive = new("run.status.doneLive", "Run done: {0} repriced, {1} listed, {2} kept, {3} skipped");
        public static readonly LocString StatusNeedsPlayer = new("run.status.needsPlayer", "Needs you: {0}");
        public static readonly LocString StatusFailed = new("run.status.failed", "The run failed: {0}");
        public static readonly LocString StatusStopped = new("run.status.stopped", "Stopped");
        public static readonly LocString BlockRunning = new("run.block.running", "A run is already going.");
        public static readonly LocString BlockNotLoggedIn = new("run.block.notLoggedIn", "Log in first.");
        public static readonly LocString BlockNotOnHomeWorld = new("run.block.notOnHomeWorld", "Go back to your home world first; retainers only sell there.");
        public static readonly LocString BlockInContent = new("run.block.inContent", "Leave the duty first.");
        public static readonly LocString BlockInCombat = new("run.block.inCombat", "Leave combat first.");
        public static readonly LocString BlockInCutscene = new("run.block.inCutscene", "Wait until the cutscene is over.");
        public static readonly LocString BlockTrading = new("run.block.trading", "Finish or cancel the trade first.");
        public static readonly LocString BlockAutoRetainerBusy = new("run.block.autoRetainerBusy", "AutoRetainer is busy; wait until it is done.");
        public static readonly LocString BlockAutoRetainerNotHeld = new("run.block.autoRetainerNotHeld", "AutoRetainer could not be held back for the run; try again, or reload AutoRetainer if this keeps happening.");
        public static readonly LocString BlockProtectListUnreadable = new("run.block.protectListUnreadable", "AutoRetainer cannot read its protect list for this character, so nothing is sold until it can. This happens when AutoRetainer has no data for the character; the Console has the details.");
        public static readonly LocString BlockUnresolvedJournal = new("run.block.unresolvedJournal", "A listing change from an earlier run is not settled yet; answer the question on the Sell page first.");
        public static readonly LocString BlockGatesClosed = new("run.block.gatesClosed", "The market checks failed when the plugin loaded, so prices cannot be read this session. The Console has the details.");
        public static readonly LocString BlockNoRules = new("run.block.noRules", "Add an item to the sell list or a category rule on the Rules page first.");
        public static readonly LocString BlockNoRetainers = new("run.block.noRetainers", "No retainer is enabled. Open a summoning bell once so Auto Seller can read your retainers.");
        public static readonly LocString BlockAuthorization = new("run.block.authorization", "The run could not be authorized. The Console has the details.");
        public static readonly LocString BlockSellListNotOpen = new("run.block.sellListNotOpen", "Open a retainer's sell list first.");
        public static readonly LocString BlockRetainerNotEnabled = new("run.block.retainerNotEnabled", "This retainer is not enabled on the Sell page.");
        public static readonly LocString BlockNothingNeeded = new("run.block.nothingNeeded", "No enabled retainer needs a visit: none has an undercut listing, a lot to list or a listing to return. Turn off Only retainers that need it on the Sell page to visit them all.");
        public static readonly LocString NeedsBellNotReached = new("run.needs.bellNotReached", "no summoning bell could be reached; stand at one or in your inn room and press Start again");
        public static readonly LocString NeedsWindowsStuck = new("run.needs.windowsStuck", "a retainer window did not open or close in time");
        public static readonly LocString NeedsUnexpectedWindow = new("run.needs.unexpectedWindow", "a window the plugin did not open is in the way");
        public static readonly LocString NeedsGilLimit = new("run.needs.gilLimit", "a retainer is close to its gil limit");
        public static readonly LocString NeedsUnknownWrite = new("run.needs.unknownWrite", "a listing change from this run is not confirmed yet; answer the question on the Sell page");
        public static readonly LocString FailedFault = new("run.failed.fault", "an unexpected error; the Console has the details");
        public static readonly LocString FailedOtherCharacter = new("run.failed.otherCharacter", "another character is logged in than the one the run started for");
        public static readonly LocString FailedMenuText = new("run.failed.menuText", "the retainer menu text could not be read from the game data");
        public static readonly LocString FailedSettingsChanged = new("run.failed.settingsChanged", "the rules, the pricing settings or the plan edits changed after Start; start again to use them");
        public static readonly LocString FailedLeftHomeWorld = new("run.failed.leftHomeWorld", "the character left its home world");
        public static readonly LocString FailedDayChanged = new("run.failed.dayChanged", "the game day changed after Start; start again for the new day");
        public static readonly LocString FailedGuard = new("run.failed.guard", "a safety check refused the run's next step; the Console has the details");
        public static readonly LocString FailedJournal = new("run.failed.journal", "the journal could not be written, so nothing more is typed; the Console has the details");
        public static readonly LocPlural ChatStarted = new("run.chat.started", "Dry run started for {0} retainer: prices are compared and nothing is listed or repriced.", "Dry run started for {0} retainers: prices are compared and nothing is listed or repriced.");
        public static readonly LocPlural ChatStartedLive = new("run.chat.startedLive", "Run started for {0} retainer: listings are repriced and lots listed within your floors and caps.", "Run started for {0} retainers: listings are repriced and lots listed within your floors and caps.");
        public static readonly LocString ChatRefused = new("run.chat.refused", "Start refused: {0}");
        public static readonly LocString ChatFaultRestart = new("run.chat.faultRestart", "The run stopped on an unexpected error; restarting it ({0} of {1}).");
        public static readonly LocString ChatKeepsMoving = new("run.chat.keepsMoving", "The character keeps moving on its own, so paths are dropped as soon as they start. Turn off the pathfinder's 'cancel path on movement input' setting, and check that no key or auto-run is held.");
    }

    internal static class History
    {
        public static readonly LocString Title = new("history.title", "History");
        public static readonly LocString Empty = new("history.empty", "Your finished runs will show up here.");
        public static readonly LocString NoRuns = new("history.noRuns", "No runs yet.");
        public static readonly LocString JustNow = new("history.time.justNow", "just now");
        public static readonly LocString MinutesAgo = new("history.time.minutesAgo", "{0}m ago");
        public static readonly LocString HoursAgo = new("history.time.hoursAgo", "{0}h ago");
        public static readonly LocString DaysAgo = new("history.time.daysAgo", "{0}d ago");
        public static readonly LocPlural Summary = new("history.summary", "{0} run recorded", "{0} runs recorded");
        public static readonly LocString OutcomeCompleted = new("history.outcome.completed", "Completed");
        public static readonly LocString OutcomePartial = new("history.outcome.partial", "Partly done");
        public static readonly LocString OutcomeStopped = new("history.outcome.stopped", "Stopped");
        public static readonly LocString OutcomeNeedsPlayer = new("history.outcome.needsPlayer", "Needs you");
        public static readonly LocString OutcomeFailed = new("history.outcome.failed", "Failed");
        public static readonly LocString DryRun = new("history.dryRun", "Dry run");
        public static readonly LocString Retainers = new("history.retainers", "{0} of {1} retainers");
        public static readonly LocString ToReprice = new("history.toReprice", "{0} to reprice");
        public static readonly LocString ToList = new("history.toList", "{0} to list");
        public static readonly LocString Repriced = new("history.repriced", "{0} repriced");
        public static readonly LocString Listed = new("history.listed", "{0} listed");
        public static readonly LocString Worth = new("history.worth", "worth {0}");
        public static readonly LocString ToReturn = new("history.toReturn", "{0} to return");
        public static readonly LocString Returned = new("history.returned", "{0} returned");
        public static readonly LocString ToVendor = new("history.toVendor", "{0} to vendor for {1}");
        public static readonly LocString Vendored = new("history.vendored", "{0} vendored for {1}");
        public static readonly LocString Kept = new("history.kept", "{0} kept");
        public static readonly LocString Skipped = new("history.skipped", "{0} skipped");
        public static readonly LocString Tooltip = new("history.tooltip", "Started {0}, ended {1}");
    }

    internal static class Plugins
    {
        public static readonly LocString Title = new("plugins.title", "Plugins");
        public static readonly LocString AllInstalled = new("plugins.allInstalled", "All required plugins are installed and loaded.");
        public static readonly LocPlural Missing = new("plugins.missing", "{0} required plugin is missing.", "{0} required plugins are missing.");
        public static readonly LocString Required = new("plugins.required", "Required");
        public static readonly LocString Optional = new("plugins.optional", "Optional");
        public static readonly LocString Installed = new("plugins.installed", "Installed");
        public static readonly LocString Install = new("plugins.install", "Install");
        public static readonly LocString Installing = new("plugins.installing", "Installing…");
        public static readonly LocString RepoHint = new("plugins.repoHint", "Repo: {0}\nLeft-click to open repo URL · right-click to copy");
        public static readonly LocString Footer = new("plugins.footer",
            "Install adds the plugin's source repository to Dalamud and queues an install. If one-click install fails (URL drift, network), right-click a plugin name to copy its repo URL and add it manually via /xlsettings -> Experimental -> Custom Plugin Repositories.");
        public static readonly LocString PurposeVnavmesh = new("plugins.purpose.vnavmesh", "Pathfinding and movement for walking to a summoning bell.");
        public static readonly LocString PurposeAutoRetainer = new("plugins.purpose.autoRetainer", "Runs Auto Seller after ventures, and shares its protect list and your alt characters.");
        public static readonly LocString PurposeMarketbuddy = new("plugins.purpose.marketbuddy", "Locked while Auto Seller types a price, so only one of them fills the sell window.");
        public static readonly LocString StateReady = new("plugins.state.ready", "Ready");
        public static readonly LocString StateBusy = new("plugins.state.busy", "Busy");
        public static readonly LocString StateOutdated = new("plugins.state.outdated", "Outdated");
        public static readonly LocString StateMissing = new("plugins.state.missing", "Not installed");
    }

    internal static class Log
    {
        public static readonly LocString Title = new("log.title", "Console");
        public static readonly LocPlural Entries = new("log.entries", "{0} line in the buffer", "{0} lines in the buffer");
        public static readonly LocString Empty = new("log.empty", "Nothing logged yet. Start a run and every step the plugin takes shows up here.");
        public static readonly LocString NoMatches = new("log.noMatches", "No lines match the current filters.");
        public static readonly LocString Footer = new("log.footer", "Every line also goes to the Dalamud log (/xllog) with the {0} prefix. When reporting a bug, press Copy log and paste the result into the issue.");
        public static readonly LocString SearchHint = new("log.searchHint", "Search messages and sources");
        public static readonly LocString CopyAll = new("log.copyAll", "Copy log");
        public static readonly LocPlural CopyFiltered = new("log.copyFiltered", "Copy {0} line", "Copy {0} lines");
        public static readonly LocString CopyTooltip = new("log.copyTooltip", "Copies the lines shown below with a header naming the plugin version, Dalamud version and zone, ready to paste into a bug report.");
        public static readonly LocString Copied = new("log.copied", "Copied");
        public static readonly LocPlural CopiedLines = new("log.copiedLines", "Copied {0} line to the clipboard", "Copied {0} lines to the clipboard");
        public static readonly LocString Clear = new("log.clear", "Clear");
        public static readonly LocString ConfirmClear = new("log.confirmClear", "Click again to clear");
        public static readonly LocString Close = new("log.close", "Close");
        public static readonly LocString LevelVerbose = new("log.level.verbose", "Verbose");
        public static readonly LocString LevelDebug = new("log.level.debug", "Debug");
        public static readonly LocString LevelInfo = new("log.level.info", "Info");
        public static readonly LocString LevelWarning = new("log.level.warning", "Warnings");
        public static readonly LocString LevelError = new("log.level.error", "Errors");
        public static readonly LocString LevelTooltip = new("log.levelTooltip", "Click to show or hide these lines. Shift-click to show only this level.");
        public static readonly LocString SourceChip = new("log.sourceChip", "Source: {0}");
        public static readonly LocString SourceChipTooltip = new("log.sourceChipTooltip", "Click to stop filtering by source.");
        public static readonly LocString JumpLatest = new("log.jumpLatest", "Jump to latest");
        public static readonly LocPlural NewLines = new("log.newLines", "{0} new line", "{0} new lines");
        public static readonly LocString Showing = new("log.showing", "Showing {0} of {1}");
        public static readonly LocString ResetFilters = new("log.resetFilters", "Reset filters");
        public static readonly LocString Shortcuts = new("log.shortcuts", "Ctrl+F search · Ctrl+C copy · Shift-click selects a range · Double-click copies a line");
        public static readonly LocPlural Selected = new("log.selected", "{0} line selected", "{0} lines selected");
        public static readonly LocString CopySelection = new("log.copySelection", "Copy selection");
        public static readonly LocString ClearSelection = new("log.clearSelection", "Clear selection");
        public static readonly LocString CopyLine = new("log.copyLine", "Copy line");
        public static readonly LocString CopyToEnd = new("log.copyToEnd", "Copy from here to the end");
        public static readonly LocString OnlySource = new("log.onlySource", "Show only {0}");
        public static readonly LocString Repeated = new("log.repeated", "Repeated {0} times in a row");
        public static readonly LocString HasDetails = new("log.hasDetails", "Has a stack trace. Select the line to read it.");
    }

    internal static class Changelog
    {
        public static readonly LocString Title = new("changelog.title", "What's new");
        public static readonly LocString Subtitle = new("changelog.subtitle", "Every update, newest first.");
        public static readonly LocString Version = new("changelog.version", "Version {0}");
        public static readonly LocString Latest = new("changelog.latest", "Latest");
        public static readonly LocString New = new("changelog.new", "New");
        public static readonly LocPlural Changes = new("changelog.changes", "{0} change", "{0} changes");

        public static readonly LocString[] Release1000 =
        [
            new("changelog.r1000.1", "Sell page: one plan across all your retainers' listings that shows what is repriced, listed, returned or vendored, at what price and why, before anything is clicked"),
            new("changelog.r1000.2", "Rules: a sell list with a floor, ceiling, fixed price, lot size, keep count, listings to keep up and preferred retainer per item, category rules, a never-sell list, and vendor rules that stay off until you turn them on"),
            new("changelog.r1000.3", "Guarded pricing: Undercut or Match against same-quality listings only, never against your own retainers or your alts, with bait listings ignored, a floor from real sales and the vendor price, a step limit, a cooldown and price-war detection"),
            new("changelog.r1000.4", "Start walks to a summoning bell or the bell in your inn room and works through every enabled retainer, or only the ones that need it, holding AutoRetainer back for the run"),
            new("changelog.r1000.5", "Every price is typed in the game's sell window, checked before Confirm and read back after, with a journal entry before each write; anything unclear stops the run and asks you"),
            new("changelog.r1000.6", "Dry run is on until you switch it off: a run does everything except type a price, and the switch next to Start asks first"),
            new("changelog.r1000.7", "AutoRetainer: runs after each venture on the retainer it hands over and always hands it back, with a Sell now button, its protect list and your alts"),
            new("changelog.r1000.8", "Assist fills in the suggested price when you open a sell window yourself and leaves Confirm to you; buttons on the retainer list and the sell list start a run right there"),
            new("changelog.r1000.9", "Sources: your bags and crystals, and by choice a retainer's own inventory, the saddlebag and the armoury chest, never items in a gear set"),
            new("changelog.r1000.10", "The undercut watch checks your listings against Universalis on a timer with no game request; the Listings page shows every listing of every character"),
            new("changelog.r1000.11", "Sales: a ledger built from sale notices, the retainers' sale histories and listings gone between visits, with daily totals, a 30-day chart, hints and a CSV export without names"),
            new("changelog.r1000.12", "Returns a listing that sat below its floor for three days and vendors junk through a retainer, each only when you turn it on"),
            new("changelog.r1000.13", "Pause, resume and stop, auto-pause in duties, a History of every run, the Console, a humanizer and a GM alert"),
        ];
    }

    internal static class About
    {
        public static readonly LocString SupportTitle = new("about.support.title", "Made with love and care");
        public static readonly LocString SupportBody = new("about.support.body", "This plugin is a one-person project, built in my free time because I love this game and its community. Keeping it updated takes a lot of those hours. If it has helped you, supporting me on Patreon or buying me a coffee means I can keep giving it that time. Thank you for being here.");
        public static readonly LocString SupportButton = new("about.support.button", "Support on Patreon");
        public static readonly LocString PatreonHint = new("about.support.hint", "Open Patreon · right-click to copy");
        public static readonly LocString CoffeeButton = new("about.support.coffeeButton", "Buy me a coffee");
        public static readonly LocString CoffeeHint = new("about.support.coffeeHint", "Open Buy Me a Coffee · right-click to copy");
        public static readonly LocString LinkHint = new("about.linkHint", "Click to open · right-click to copy");
        public static readonly LocString MadeBy = new("about.madeBy", "Made by {0}");
        public static readonly LocString Version = new("about.version", "v {0}");
        public static readonly LocString Community = new("about.community", "Community");
        public static readonly LocString DiscordTitle = new("about.discordTitle", "Join the Discord");
        public static readonly LocString DiscordBody = new("about.discordBody", "Get help, report bugs, share ideas and hear about updates first.");
        public static readonly LocString GitHubTitle = new("about.githubTitle", "View on GitHub");
        public static readonly LocString GitHubBody = new("about.githubBody", "Browse the source code and every release.");
        public static readonly LocString ReminderTitle = new("about.reminder.title", "A little reminder");
        public static readonly LocString FactsTitle = new("about.facts.title", "Did you know?");
        public static readonly LocString QuotesTitle = new("about.quotes.title", "Words to live by");
        public static readonly LocString JokesTitle = new("about.jokes.title", "Just for fun");

        public static readonly LocString[] Reminders =
        [
            new("about.reminder.1", "Been at it a while? Roll your shoulders and take one slow breath."),
            new("about.reminder.2", "Hydration check. When did you last drink some water?"),
            new("about.reminder.3", "Blink a few times and let your eyes rest for a moment."),
            new("about.reminder.4", "Stand up, stretch, and shake out your hands. Future you says thanks."),
            new("about.reminder.5", "Sit up and settle in comfortably. Your back will thank you later."),
            new("about.reminder.6", "Remember to eat something today. You matter more than any score."),
            new("about.reminder.7", "Eyes feel tired? Look at something far away for twenty seconds."),
            new("about.reminder.8", "Whatever you're chasing, you're allowed to take a break whenever."),
            new("about.reminder.9", "You're doing great. Be a little kinder to yourself today."),
            new("about.reminder.10", "A glass of water and a quick stretch can reset a long session."),
            new("about.reminder.11", "Unclench your jaw and drop your shoulders. There you go."),
            new("about.reminder.12", "Rest is part of the journey too. Step away whenever you need to."),
        ];

        public static readonly LocString[] Facts =
        [
            new("about.facts.1", "Honey never spoils. Jars over 3,000 years old have been found still edible."),
            new("about.facts.2", "Octopuses have three hearts and blue blood."),
            new("about.facts.3", "A day on Venus is longer than a whole year on Venus."),
            new("about.facts.4", "Bananas are berries, but strawberries aren't."),
            new("about.facts.5", "There are more possible chess games than atoms in the observable universe."),
            new("about.facts.6", "Sharks have been around longer than trees have."),
            new("about.facts.7", "A group of flamingos is called a flamboyance."),
            new("about.facts.8", "Honeybees can recognize individual human faces."),
            new("about.facts.9", "Wombat droppings are cube shaped."),
            new("about.facts.10", "The Eiffel Tower can grow over 15 cm taller on a hot day."),
            new("about.facts.11", "Hot water can sometimes freeze faster than cold water."),
            new("about.facts.12", "A bolt of lightning is roughly five times hotter than the surface of the Sun."),
        ];

        public static readonly LocString[] Quotes =
        [
            new("about.quotes.1", "Done is better than perfect. You can always polish later."),
            new("about.quotes.2", "Small steps every day add up to surprising distances."),
            new("about.quotes.3", "Comparison is the thief of joy. Run your own race."),
            new("about.quotes.4", "Progress, not perfection."),
            new("about.quotes.5", "You don't have to be great to start, but you have to start to be great."),
            new("about.quotes.6", "Be patient with yourself. Growth takes time."),
            new("about.quotes.7", "The best time to begin was yesterday. The second best is right now."),
            new("about.quotes.8", "Celebrate the small wins. They count too."),
            new("about.quotes.9", "Slow progress is still progress."),
            new("about.quotes.10", "Your only real competition is who you were yesterday."),
        ];

        public static readonly LocString[] Jokes =
        [
            new("about.jokes.1", "Why don't scientists trust atoms? Because they make up everything."),
            new("about.jokes.2", "I would tell you a chemistry joke, but I know I wouldn't get a reaction."),
            new("about.jokes.3", "Why did the scarecrow win an award? He was outstanding in his field."),
            new("about.jokes.4", "I'm reading a book about anti-gravity. It's impossible to put down."),
            new("about.jokes.5", "Why don't skeletons fight each other? They don't have the guts."),
            new("about.jokes.6", "What do you call fake spaghetti? An impasta."),
            new("about.jokes.7", "Why did the bicycle fall over? It was two tired."),
            new("about.jokes.8", "What do you call cheese that isn't yours? Nacho cheese."),
            new("about.jokes.9", "I'm on a seafood diet. I see food, and I eat it."),
            new("about.jokes.10", "I only know 25 letters of the alphabet. I don't know y."),
        ];
    }

    internal static class Settings
    {
        public static readonly LocString Title = new("settings.title", "Settings");
        public static readonly LocString Language = new("settings.language", "Language");
        public static readonly LocString LanguageHelp = new("settings.languageHelp", "The language of this plugin's windows. Zone, item and world names always follow the game client.");
        public static readonly LocString SearchHint = new("settings.searchHint", "Search...");

        public static readonly LocString CatGeneral = new("settings.cat.general", "General");
        public static readonly LocString CatGeneralSub = new("settings.cat.generalSub", "Window and behavior preferences.");

        public static readonly LocString GeneralWindow = new("settings.general.window", "Window");
        public static readonly LocString OpenOnLogin = new("settings.general.openOnLogin", "Open on login");
        public static readonly LocString OpenOnLoginHelp = new("settings.general.openOnLoginHelp", "Pop the main window automatically the next time you log in.");
        public static readonly LocString GeneralBehavior = new("settings.general.behavior", "Behavior");
        public static readonly LocString AutoPause = new("settings.general.autoPause", "Auto-pause in content");
        public static readonly LocString AutoPauseHelp = new("settings.general.autoPauseHelp", "Pause the run while you are inside a duty, trial, raid or any other instanced content, and carry on where it stopped once you are back outside.");
        public static readonly LocString PauseTyping = new("settings.general.pauseTyping", "Hold while typing");
        public static readonly LocString PauseTypingHelp = new("settings.general.pauseTypingHelp", "While a text box of the game has focus, like the chat line, a run starts no new move until you close it. A move already under way carries on.");
    }

    internal static class Plugin
    {
        public static readonly LocString CommandHelp = new("plugin.commandHelp", "Toggle the Auto Seller window. /asell config | stats | sales | deps | log | changelog | about | start (start the run) | stop (stop the run) | pause (pause or resume the run) | help (list these commands in the chat) | target (log the targeted NPC's BaseId) | look <item id> (compare market prices once and log the price inputs) | mbstatus (log the market checks, the search state and the open retainer's market) | sendpath compare|proxy (send market searches through the sell window or the item search proxy) | historypath window|packet (read sale histories from the Sale History window or from the data the game receives for it; packet reads the window until its handler is found in game).");
        public static readonly LocString CommandHelpAlias = new("plugin.commandHelpAlias", "Alias for /asell.");
        public static readonly LocString ConfigReset = new("plugin.configReset", "Your settings could not be read, so Auto Seller started with default settings. The old file is kept as {0}.");
        public static readonly LocString ConfigResetUnsaved = new("plugin.configResetUnsaved", "Your settings could not be read, so Auto Seller started with default settings. The old file could not be kept; the log has the details.");
        public static readonly LocString TargetTerritory = new("plugin.target.territory", "Territory: {0} ({1})");
        public static readonly LocString TargetNone = new("plugin.target.none", "No target. Click an NPC or an object first, then run /asell target again.");
        public static readonly LocString TargetFound = new("plugin.target.found", "Target: BaseId={0}  Name=\"{1}\"");
    }
}
