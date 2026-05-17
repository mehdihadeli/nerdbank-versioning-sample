using System.Reflection;
using System.Text.Json;

namespace versioning_samples;

public static class VersionInfoProvider
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new() { PropertyNameCaseInsensitive = true };

    public static AppVersionInfo GetVersionInfo()
    {
        var publishedVersion = TryGetPublishedVersion();
        if (publishedVersion is not null)
        {
            return publishedVersion;
        }

        var environmentVersion = TryGetEnvironmentVersion();
        if (environmentVersion is not null)
        {
            return environmentVersion;
        }

        var entryAssembly = Assembly.GetEntryAssembly();
        var informationalVersion =
            entryAssembly
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? ThisAssembly.AssemblyInformationalVersion;

        var assemblyVersion =
            entryAssembly?.GetName().Version?.ToString() ?? ThisAssembly.AssemblyVersion;
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        if (
            informationalVersion.EndsWith("preview.0", StringComparison.OrdinalIgnoreCase)
            && HasGitDirectoryNearby()
        )
        {
            return new AppVersionInfo(
                informationalVersion,
                informationalVersion,
                informationalVersion,
                assemblyVersion,
                null,
                null,
                null,
                environment,
                "assembly metadata from Nerdbank.GitVersioning (git repo exists, but HEAD has no commits yet)"
            );
        }

        return new AppVersionInfo(
            informationalVersion,
            informationalVersion,
            informationalVersion,
            assemblyVersion,
            null,
            null,
            null,
            environment,
            "assembly metadata from Nerdbank.GitVersioning"
        );
    }

    private static AppVersionInfo? TryGetPublishedVersion()
    {
        var versionFilePath = Path.Combine(AppContext.BaseDirectory, "version.json");

        if (!File.Exists(versionFilePath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(versionFilePath);
            var published = JsonSerializer.Deserialize<PublishedVersionFile>(
                stream,
                SerializerOptions
            );

            if (string.IsNullOrWhiteSpace(published?.SemVer))
            {
                return null;
            }

            return new AppVersionInfo(
                published.SemVer,
                published.InformationalVersion ?? published.SemVer,
                published.NbgvSemVer ?? published.SemVer,
                published.AssemblyVersion,
                published.DateStamp,
                published.Revision,
                published.Commit,
                published.Environment,
                "version.json (CI/CD effective version)"
            );
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

    private static AppVersionInfo? TryGetEnvironmentVersion()
    {
        var semVer = Environment.GetEnvironmentVariable("APP_SEMVER");
        if (string.IsNullOrWhiteSpace(semVer))
        {
            return null;
        }

        return new AppVersionInfo(
            semVer,
            Environment.GetEnvironmentVariable("APP_INFORMATIONAL_VERSION") ?? semVer,
            Environment.GetEnvironmentVariable("APP_NBGV_SEMVER") ?? semVer,
            Environment.GetEnvironmentVariable("APP_ASSEMBLY_VERSION"),
            Environment.GetEnvironmentVariable("APP_DATE_STAMP"),
            Environment.GetEnvironmentVariable("APP_REVISION"),
            Environment.GetEnvironmentVariable("APP_COMMIT"),
            Environment.GetEnvironmentVariable("APP_DEPLOYMENT_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "environment variables (CI/CD effective version)"
        );
    }

    private static bool HasGitDirectoryNearby()
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
}

public sealed record AppVersionInfo(
    string SemVer,
    string InformationalVersion,
    string? NbgvSemVer,
    string? AssemblyVersion,
    string? DateStamp,
    string? Revision,
    string? Commit,
    string? Environment,
    string Source
);

internal sealed class PublishedVersionFile
{
    public string? NbgvSemVer { get; init; }

    public string? SemVer { get; init; }

    public string? InformationalVersion { get; init; }

    public string? AssemblyVersion { get; init; }

    public string? DateStamp { get; init; }

    public string? Revision { get; init; }

    public string? Commit { get; init; }

    public string? Environment { get; init; }
}
