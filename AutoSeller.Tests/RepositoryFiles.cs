using System.IO;

namespace AutoSeller.Tests;

internal static class RepositoryFiles
{
    private const string SolutionFileName = "AutoSeller.sln";
    private const string CatalogExtension = ".json";

    public static readonly string[] LanguageCodes = ["en", "de", "es", "fr", "ja", "pt", "ru", "tr", "zh"];

    public static string CatalogDirectory => Path.Combine(AppContext.BaseDirectory, "Localization");

    public static string CatalogPath(string languageCode)
    {
        return Path.Combine(CatalogDirectory, string.Concat(languageCode, CatalogExtension));
    }

    public static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new DirectoryNotFoundException($"{SolutionFileName} was not found above {AppContext.BaseDirectory}.");
        }
    }
}
