using AutoSeller.Core.Localization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AutoSeller.Tests;

public sealed partial class LocalizationCatalogTests
{
    private const string EnglishCode = "en";
    private const char EmDash = '\u2014';

    public static TheoryData<string> TranslatedLanguages()
    {
        var data = new TheoryData<string>();
        for (var index = 0; index < RepositoryFiles.LanguageCodes.Length; index++)
        {
            if (RepositoryFiles.LanguageCodes[index] != EnglishCode)
            {
                data.Add(RepositoryFiles.LanguageCodes[index]);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(TranslatedLanguages))]
    public void CatalogKeepsTheEnglishKeysInTheSameOrder(string languageCode)
    {
        var english = ReadKeys(EnglishCode);
        var translated = ReadKeys(languageCode);
        Assert.Equal(english, translated);
    }

    [Fact]
    public void EnglishCatalogHoldsExactlyTheStringTableKeys()
    {
        var declared = DeclaredKeys();
        var catalog = new HashSet<string>(ReadKeys(EnglishCode), StringComparer.Ordinal);
        Assert.True(declared.SetEquals(catalog), $"Only in L: {string.Join(", ", Except(declared, catalog))}. Only in en.json: {string.Join(", ", Except(catalog, declared))}.");
    }

    [Theory]
    [MemberData(nameof(TranslatedLanguages))]
    public void TranslationsKeepTheEnglishPlaceholders(string languageCode)
    {
        var english = ReadCatalog(EnglishCode);
        var translated = ReadCatalog(languageCode);
        for (var index = 0; index < english.Count; index++)
        {
            var (key, englishText) = english[index];
            Assert.True(SamePlaceholders(englishText, translated[index].Value), $"{languageCode}.json changes the placeholders of {key}.");
        }
    }

    [Fact]
    public void NoCatalogHoldsAnEmDash()
    {
        for (var languageIndex = 0; languageIndex < RepositoryFiles.LanguageCodes.Length; languageIndex++)
        {
            var entries = ReadCatalog(RepositoryFiles.LanguageCodes[languageIndex]);
            for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                Assert.True(entries[entryIndex].Value.IndexOf(EmDash) < 0, $"{RepositoryFiles.LanguageCodes[languageIndex]}.json has an em dash in {entries[entryIndex].Key}.");
            }
        }
    }

    private static List<string> ReadKeys(string languageCode)
    {
        var entries = ReadCatalog(languageCode);
        var keys = new List<string>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            keys.Add(entries[index].Key);
        }

        return keys;
    }

    private static List<KeyValuePair<string, string>> ReadCatalog(string languageCode)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepositoryFiles.CatalogPath(languageCode)));
        var entries = new List<KeyValuePair<string, string>>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            entries.Add(new KeyValuePair<string, string>(property.Name, property.Value.GetString() ?? string.Empty));
        }

        return entries;
    }

    private static HashSet<string> DeclaredKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var groups = typeof(L).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
        for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            var fields = groups[groupIndex].GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            for (var fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
            {
                AddKeys(keys, fields[fieldIndex].GetValue(null));
            }
        }

        return keys;
    }

    private static void AddKeys(HashSet<string> keys, object? value)
    {
        switch (value)
        {
            case LocString entry:
                keys.Add(entry.Key);
                break;
            case LocPlural plural:
                keys.Add(string.Concat(plural.KeyBase, ".one"));
                keys.Add(string.Concat(plural.KeyBase, ".other"));
                break;
            case LocString[] entries:
                for (var index = 0; index < entries.Length; index++)
                {
                    keys.Add(entries[index].Key);
                }

                break;
        }
    }

    private static List<string> Except(HashSet<string> source, HashSet<string> removed)
    {
        var remaining = new List<string>();
        foreach (var key in source)
        {
            if (!removed.Contains(key))
            {
                remaining.Add(key);
            }
        }

        return remaining;
    }

    private static bool SamePlaceholders(string english, string translated)
    {
        var expected = Placeholders(english);
        var actual = Placeholders(translated);
        return expected.SetEquals(actual);
    }

    private static HashSet<string> Placeholders(string text)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        var matches = PlaceholderPattern().Matches(text);
        for (var index = 0; index < matches.Count; index++)
        {
            found.Add(matches[index].Value);
        }

        return found;
    }

    [GeneratedRegex(@"\{\d+\}|%[.\d]*[a-z]")]
    private static partial Regex PlaceholderPattern();
}
