using AutoSeller.Core.Localization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AutoSeller.Tests;

public sealed partial class CommandHelpTests
{
    private const string Command = "/asell ";
    private const string HelpSeparator = " | ";
    private const string CommandsHeading = "## Commands";
    private const string NextHeading = "## Languages";
    private const string ReadmeFileName = "README.md";

    public static TheoryData<string> Languages()
    {
        return new TheoryData<string>(RepositoryFiles.LanguageCodes);
    }

    [Fact]
    public void TheReadmeTableAndTheCommandHelpListTheSameSubcommands()
    {
        AssertSameSubcommands(L.Plugin.CommandHelp.Source, L.Plugin.CommandHelp.Key);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void TheCommandHelpOfEveryCatalogListsTheReadmeSubcommands(string languageCode)
    {
        AssertSameSubcommands(CatalogText(languageCode, L.Plugin.CommandHelp.Key), $"{languageCode}.json");
    }

    private static void AssertSameSubcommands(string help, string helpSource)
    {
        var documented = ReadmeSubcommands();
        var helped = HelpSubcommands(help);

        Assert.NotEmpty(helped);
        Assert.True(documented.SetEquals(helped), $"Only in the README: {string.Join(", ", Except(documented, helped))}. Only in the command help of {helpSource}: {string.Join(", ", Except(helped, documented))}.");
    }

    private static string CatalogText(string languageCode, string key)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepositoryFiles.CatalogPath(languageCode)));
        return document.RootElement.GetProperty(key).GetString() ?? string.Empty;
    }

    private static HashSet<string> ReadmeSubcommands()
    {
        var readme = File.ReadAllText(Path.Combine(RepositoryFiles.Root, ReadmeFileName));
        var start = readme.IndexOf(CommandsHeading, StringComparison.Ordinal);
        var end = readme.IndexOf(NextHeading, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "The README has no Commands section before Languages.");

        var table = readme[start..end];
        var subcommands = new HashSet<string>(StringComparer.Ordinal);
        var matches = ReadmeRowPattern().Matches(table);
        for (var index = 0; index < matches.Count; index++)
        {
            subcommands.Add(matches[index].Groups[1].Value);
        }

        return subcommands;
    }

    private static HashSet<string> HelpSubcommands(string help)
    {
        var subcommands = new HashSet<string>(StringComparer.Ordinal);
        var listStart = help.IndexOf(Command, StringComparison.Ordinal);
        if (listStart < 0)
        {
            return subcommands;
        }

        var segments = help[(listStart + Command.Length)..].Split(HelpSeparator);
        for (var index = 0; index < segments.Length; index++)
        {
            var word = LeadingWordPattern().Match(segments[index]);
            if (word.Success)
            {
                subcommands.Add(word.Value);
            }
        }

        return subcommands;
    }

    private static List<string> Except(HashSet<string> source, HashSet<string> removed)
    {
        var remaining = new List<string>();
        foreach (var entry in source)
        {
            if (!removed.Contains(entry))
            {
                remaining.Add(entry);
            }
        }

        return remaining;
    }

    [GeneratedRegex(@"^\| `/asell ([a-z]+)", RegexOptions.Multiline)]
    private static partial Regex ReadmeRowPattern();

    [GeneratedRegex(@"^[a-z]+")]
    private static partial Regex LeadingWordPattern();
}
