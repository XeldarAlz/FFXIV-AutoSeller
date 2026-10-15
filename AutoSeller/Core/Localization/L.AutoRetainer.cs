namespace AutoSeller.Core.Localization;

internal static partial class L
{
    internal static class AutoRetainer
    {
        public static readonly LocString Tab = new("autoRetainer.tab", "AutoRetainer");
        public static readonly LocString TabSub = new("autoRetainer.tabSub", "Work alongside AutoRetainer when it is installed.");
        public static readonly LocString Group = new("autoRetainer.group", "Together with AutoRetainer");
        public static readonly LocString Postprocess = new("autoRetainer.postprocess", "Run after ventures");
        public static readonly LocString PostprocessHelp = new("autoRetainer.postprocessHelp", "When AutoRetainer finishes a retainer's venture, reprice and list on that retainer before handing it back.");
        public static readonly LocString SellNow = new("autoRetainer.sellNow", "Sell now button");
        public static readonly LocString SellNowHelp = new("autoRetainer.sellNowHelp", "Add a button to AutoRetainer's retainer list that sends every retainer through Auto Seller.");
        public static readonly LocString SellNowTooltip = new("autoRetainer.sellNowTooltip", "Auto Seller: reprice and list on each retainer now");
        public static readonly LocString SellNowTooltipDryRun = new("autoRetainer.sellNowTooltipDryRun", "Auto Seller: dry run over each retainer now; nothing is typed");
        public static readonly LocString SellNowBusy = new("autoRetainer.sellNowBusy", "Auto Seller is already running");
        public static readonly LocString SellNowUnavailable = new("autoRetainer.sellNowUnavailable", "Off while AutoRetainer is outdated or not answering");
        public static readonly LocString Alts = new("autoRetainer.alts", "Treat AutoRetainer characters as my alts");
        public static readonly LocString AltsHelp = new("autoRetainer.altsHelp", "Never undercut listings from the retainers of your other characters that AutoRetainer knows.");
        public static readonly LocString Footnote = new("autoRetainer.footnote", "These need AutoRetainer installed and up to date. Items AutoRetainer protects are never sold or vendored, whatever these switches say.");
        public static readonly LocString StatusGroup = new("autoRetainer.statusGroup", "Right now");
        public static readonly LocString State = new("autoRetainer.state", "AutoRetainer");
        public static readonly LocString StateHelp = new("autoRetainer.stateHelp", "Whether AutoRetainer answers. Outdated means Auto Seller runs only from Start and does not hold AutoRetainer back.");
        public static readonly LocString AltsKnown = new("autoRetainer.altsKnown", "Alts known");
        public static readonly LocString AltsKnownHelp = new("autoRetainer.altsKnownHelp", "Your other characters that AutoRetainer knows, and their retainers, read again each time the plan is built.");
        public static readonly LocPlural AltCharacters = new("autoRetainer.altCharacters", "{0} character", "{0} characters");
        public static readonly LocString AltsOff = new("autoRetainer.altsOff", "Not counted");
        public static readonly LocString Job = new("autoRetainer.job", "After ventures");
        public static readonly LocString JobHelp = new("autoRetainer.jobHelp", "What Auto Seller is doing in AutoRetainer's venture cycle.");
        public static readonly LocString JobOff = new("autoRetainer.job.off", "Off");
        public static readonly LocString JobWaiting = new("autoRetainer.job.waiting", "Waiting for AutoRetainer");
        public static readonly LocString JobStoodDown = new("autoRetainer.job.stoodDown", "Stopped by you at this bell");
        public static readonly LocString JobRequested = new("autoRetainer.job.requested", "Asked for {0}");
        public static readonly LocString JobWorking = new("autoRetainer.job.working", "Working on {0}");
        public static readonly LocString JobReturning = new("autoRetainer.job.returning", "Handing {0} back");
        public static readonly LocString Held = new("autoRetainer.held", "AutoRetainer is held back until the current run ends.");
    }
}
