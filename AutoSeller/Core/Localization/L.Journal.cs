namespace AutoSeller.Core.Localization;

internal static partial class L
{
    internal static class Journal
    {
        public static readonly LocString DamagedTitle = new("journal.damagedTitle", "The journal needs a fresh start");
        public static readonly LocString DamagedBody = new("journal.damagedBody", "The journal of listing changes holds lines that cannot be trusted, so no price is written. Set it aside to start a fresh one; the old file is kept beside it.");
        public static readonly LocString UnreadableBody = new("journal.unreadableBody", "The journal of listing changes could not be read, so no price is written. The Console has the details.");
        public static readonly LocString SetAside = new("journal.setAside", "Set it aside");
        public static readonly LocString SetAsideFailed = new("journal.setAsideFailed", "The journal could not be set aside; the Console has the details.");
        public static readonly LocString ListTitle = new("journal.listTitle", "Did this listing go up at {0}?");
        public static readonly LocString RepriceTitle = new("journal.repriceTitle", "Did this listing change to {0}?");
        public static readonly LocString ReturnTitle = new("journal.returnTitle", "Did this listing go back to the retainer?");
        public static readonly LocString VendorTitle = new("journal.vendorTitle", "Did the retainer sell this stack for {0}?");
        public static readonly LocString Body = new("journal.body", "A run could not confirm this change after pressing Confirm, so no price is written until you answer. If you are not sure, look at the retainer's sell list at a summoning bell.");
        public static readonly LocString ListFacts = new("journal.listFacts", "{0} x{1} on {2}, {3}");
        public static readonly LocString RepriceFacts = new("journal.repriceFacts", "{0} x{1} on {2}, from {3} to {4}, {5}");
        public static readonly LocString VendorFacts = new("journal.vendorFacts", "{0} x{1} sold through {2}, {3}");
        public static readonly LocString HintApplied = new("journal.hintApplied", "A later read of {0} shows the change went through.");
        public static readonly LocString HintNotApplied = new("journal.hintNotApplied", "A later read of {0} shows no change.");
        public static readonly LocString HintUnclear = new("journal.hintUnclear", "A later read of {0} cannot tell.");
        public static readonly LocString HintNoRead = new("journal.hintNoRead", "Open the sell list of {0} at a summoning bell and it is read to suggest the answer.");
        public static readonly LocString WentThrough = new("journal.wentThrough", "It went through");
        public static readonly LocString DidNotGoThrough = new("journal.didNotGoThrough", "It did not");
        public static readonly LocString AnswerFailed = new("journal.answerFailed", "The answer could not be written to the journal; the Console has the details.");
        public static readonly LocString UnknownRetainer = new("journal.unknownRetainer", "a retainer");
    }
}
