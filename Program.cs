using System.Reflection;
using System.Text.Json;

var (version, source) = GetVersionInfo();

Console.WriteLine("Hello, World!");
Console.WriteLine($"Application version: {version}");
Console.WriteLine($"Version source: {source}");

static (string Version, string Source) GetVersionInfo()
{
    var publishedVersion = TryGetPublishedVersion();
    if (!string.IsNullOrWhiteSpace(publishedVersion))
    {
        return (publishedVersion, "publish/version.json (CI/CD effective version)");
    }

    var assemblyVersion =
        Assembly
            .GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? ThisAssembly.AssemblyInformationalVersion;

    if (
        assemblyVersion.EndsWith("preview.0", StringComparison.OrdinalIgnoreCase)
        && HasGitDirectoryNearby()
    )
    {
        return (
            assemblyVersion,
            "assembly metadata from Nerdbank.GitVersioning (git repo exists, but HEAD has no commits yet)"
        );
    }

    return (assemblyVersion, "assembly metadata from Nerdbank.GitVersioning");
}

static string? TryGetPublishedVersion()
{
    var versionFilePath = Path.Combine(AppContext.BaseDirectory, "version.json");

    if (!File.Exists(versionFilePath))
    {
        return null;
    }

    try
    {
        using var stream = File.OpenRead(versionFilePath);
        using var document = JsonDocument.Parse(stream);

        return document.RootElement.TryGetProperty("semVer", out var semVerProperty)
            ? semVerProperty.GetString()
            : null;
    }
    catch (JsonException)
    {
        return null;
    }
    catch (IOException)
    {
        return null;
    }
}

static bool HasGitDirectoryNearby()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);

    while (directory is not null)
    {
        if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            return true;
        }

        directory = directory.Parent;
    }

    return false;
}
