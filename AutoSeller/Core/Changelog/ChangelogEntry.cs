using AutoSeller.Core.Localization;

namespace AutoSeller.Core.Changelog;

internal readonly record struct ChangelogEntry(string Version, string Date, LocString[] Highlights);
