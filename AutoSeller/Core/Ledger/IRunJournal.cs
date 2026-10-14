using AutoSeller.Core.Execution.Model;

namespace AutoSeller.Core.Ledger;

internal interface IRunJournal
{
    long NextSequence { get; }

    string? LastError { get; }

    bool TryAppend(JournalEvent journalEvent);

    JournalReadResult Read();
}
