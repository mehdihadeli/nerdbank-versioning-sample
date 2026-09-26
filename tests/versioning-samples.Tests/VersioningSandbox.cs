using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace versioning_samples.Tests;

internal sealed class VersioningSandbox : IDisposable
{
    private readonly string _rootDirectory;

    private VersioningSandbox(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
    }

    public string RootDirectory => _rootDirectory;

    public static VersioningSandbox Create()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sandboxPath = Path.Combine(
            Path.GetTempPath(),
            $"versioning-samples-tests-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(sandboxPath);

        File.Copy(
            Path.Combine(repositoryRoot, "version.json"),
            Path.Combine(sandboxPath, "version.json"),
            overwrite: true
        );
        File.Copy(
            Path.Combine(repositoryRoot, "dotnet-tools.json"),
            Path.Combine(sandboxPath, "dotnet-tools.json"),
            overwrite: true
        );
        File.Copy(
            Path.Combine(repositoryRoot, "release-version.sh"),
            Path.Combine(sandboxPath, "release-version.sh"),
            overwrite: true
        );

        var sandbox = new VersioningSandbox(sandboxPath);
        sandbox.InitializePreviewVersion();

        sandbox.Run("git", "init");
        sandbox.Run("git", "config", "user.email", "test@test.com");
        sandbox.Run("git", "config", "user.name", "Test");
        sandbox.Run("git", "checkout", "-b", "main");
        sandbox.Run("dotnet", "tool", "restore");

        File.WriteAllText(Path.Combine(sandboxPath, ".gitignore"), "bin/\nobj/\n", Encoding.UTF8);
        File.WriteAllText(Path.Combine(sandboxPath, "changes.txt"), "seed\n", Encoding.UTF8);

        sandbox.Run("git", "add", ".");
        sandbox.Run("git", "commit", "-m", "chore: initial project setup", "--no-verify");

        return sandbox;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootDirectory))
            {
                Directory.Delete(_rootDirectory, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup for temporary test sandbox
        }
    }

    public string GetSemVer2() => Run("dotnet", "nbgv", "get-version", "-v", "SemVer2");

    public string RunReleaseVersionScript(params string[] arguments)
    {
        var bashPath = FindExecutable(
            OperatingSystem.IsWindows() ? "bash.exe" : "bash",
            OperatingSystem.IsWindows()
                ? new[]
                {
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        "Git",
                        "usr",
                        "bin",
                        "bash.exe"
                    ),
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                        "Git",
                        "usr",
                        "bin",
                        "bash.exe"
                    ),
                }
                : []
        );
        var dotnetPath = FindExecutable(OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        var dotnetDirectory =
            Path.GetDirectoryName(dotnetPath)
            ?? throw new InvalidOperationException("Unable to locate the dotnet directory.");
        var bashDirectory =
            Path.GetDirectoryName(bashPath)
            ?? throw new InvalidOperationException("Unable to locate the Bash directory.");

        return RunWithPath(
            string.Join(Path.PathSeparator, dotnetDirectory, bashDirectory),
            bashPath,
            ["release-version.sh", .. arguments]
        );
    }

    private static string FindExecutable(
        string executableName,
        IEnumerable<string>? preferredPaths = null
    )
    {
        foreach (var preferredPath in preferredPaths ?? [])
        {
            if (File.Exists(preferredPath))
            {
                return preferredPath;
            }
        }

        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in pathVariable.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            var executablePath = Path.Combine(directory, executableName);
            if (File.Exists(executablePath))
            {
                return executablePath;
            }
        }

        throw new InvalidOperationException(
            $"Unable to locate '{executableName}' in the PATH environment variable."
        );
    }

    public string CalculatePullRequestValidation() => GetSemVer2();

    public bool WouldPublishPullRequest() => false;

    public string CalculateMainPreview() => GetSemVer2();

    public string CalculateSemanticVersion()
    {
        var version = GetSemVer2();
        var gitCommitSuffixIndex = version.IndexOf("-g", StringComparison.Ordinal);
        if (gitCommitSuffixIndex >= 0)
        {
            version = version[..gitCommitSuffixIndex];
        }

        var commitMetadataIndex = version.IndexOf(".g", StringComparison.Ordinal);
        if (commitMetadataIndex >= 0)
        {
            version = version[..commitMetadataIndex];
        }

        var buildMetadataIndex = version.IndexOf('+', StringComparison.Ordinal);
        return buildMetadataIndex < 0 ? version : version[..buildMetadataIndex];
    }

    public string GetAssemblyInformationalVersion() =>
        Run("dotnet", "nbgv", "get-version", "-v", "AssemblyInformationalVersion");

    public int GetRevision() =>
        int.Parse(Run("git", "rev-list", "--count", "HEAD"), CultureInfo.InvariantCulture);

    public string GetCommitShort() => Run("git", "rev-parse", "--short", "HEAD");

    public string GetExpectedDockerTag(string effectiveSemVer) => effectiveSemVer.Replace('+', '-');

    public void CreateBranch(string branchName) => Run("git", "checkout", "-b", branchName);

    public void Checkout(string branchName) => Run("git", "checkout", branchName);

    public void CommitChange(string message)
    {
        File.AppendAllText(
            Path.Combine(_rootDirectory, "changes.txt"),
            $"{message} - {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n",
            Encoding.UTF8
        );
        Run("git", "add", "changes.txt");
        Run("git", "commit", "-m", message, "--no-verify");
    }

    public void CommitVersionChange(string message)
    {
        Run("git", "add", "version.json");
        Run("git", "commit", "-m", message, "--no-verify");
    }

    public void MergeSquashToMain(string branchName)
    {
        Checkout("main");
        Run("git", "merge", "--squash", branchName);
        Run("git", "commit", "-m", $"Merge {branchName}", "--no-verify");
        Run("git", "branch", "-D", branchName);
    }

    /// <summary>
    /// Commits an RC version change using Git height before the RC tag is created.
    /// </summary>
    public void PrepareStaging(string stableVersion)
    {
        var target = $"{stableVersion}-rc.{{height}}";
        Run("dotnet", "nbgv", "set-version", target);
        Run("git", "add", "version.json");
        Run(
            "git",
            "commit",
            "-m",
            $"chore(version): set {stableVersion}-rc from height",
            "--no-verify"
        );
    }

    public void PrepareProduction(string stableVersion)
    {
        SetVersionValue(stableVersion);
        RemoveVersionHeightOffset();
        Run("git", "add", "version.json");
        Run("git", "commit", "-m", $"chore(version): release {stableVersion}", "--no-verify");
    }

    public void PrepareNextPreviewTrain(string nextStableVersion)
    {
        var target = $"{nextStableVersion}-preview.{{height}}";
        SetVersionValue(target);
        RemoveVersionHeightOffset();
        Run("git", "add", "version.json");
        Run("git", "commit", "-m", $"chore(version): start {nextStableVersion}", "--no-verify");
    }

    public void CreateTag(string tagName, string? message = null)
    {
        Run("git", "tag", "-a", tagName, "-m", message ?? $"Release {tagName}");
    }

    /// <summary>
    /// Creates an NBGV tag for the version committed at the current HEAD.
    /// </summary>
    public void CreateNbgvTag() => Run("dotnet", "nbgv", "tag");

    public string? GetTagPointingAtHead(string pattern)
    {
        var tags = RunAllowFailure("git", "tag", "--points-at", "HEAD")
            .Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );

        return tags.FirstOrDefault(tag =>
            System.Text.RegularExpressions.Regex.IsMatch(tag, pattern)
        );
    }

    private void SetVersionValue(string newVersion)
    {
        var versionFilePath = Path.Combine(_rootDirectory, "version.json");
        var document =
            JsonNode.Parse(File.ReadAllText(versionFilePath, Encoding.UTF8))
            ?? throw new InvalidOperationException("Unable to parse version.json.");

        document["version"] = newVersion;
        File.WriteAllText(
            versionFilePath,
            document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        );
    }

    private void InitializePreviewVersion()
    {
        var versionFilePath = Path.Combine(_rootDirectory, "version.json");
        var document =
            JsonNode.Parse(File.ReadAllText(versionFilePath, Encoding.UTF8))
            ?? throw new InvalidOperationException("Unable to parse version.json.");

        document["version"] = "1.0.0-preview.{height}";
        document["versionHeightOffset"] = -1;
        document["versionHeightOffsetAppliesTo"] = "1.0.0-preview.{height}";
        File.WriteAllText(
            versionFilePath,
            document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        );
    }

    private void RemoveVersionHeightOffset()
    {
        var versionFilePath = Path.Combine(_rootDirectory, "version.json");
        var document =
            JsonNode.Parse(File.ReadAllText(versionFilePath, Encoding.UTF8))
            ?? throw new InvalidOperationException("Unable to parse version.json.");

        var versionObject = document.AsObject();
        versionObject.Remove("versionHeightOffset", out _);
        versionObject.Remove("versionHeightOffsetAppliesTo", out _);
        File.WriteAllText(
            versionFilePath,
            document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        );
    }

    private string Run(string fileName, params string[] arguments)
    {
        return RunWithPath(null, fileName, arguments);
    }

    private string RunWithPath(string? pathPrefix, string fileName, params string[] arguments)
    {
        var (exitCode, standardOutput, standardError) = RunProcess(fileName, arguments, pathPrefix);
        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"Command failed ({fileName} {string.Join(' ', arguments)}).\nSTDOUT:\n{standardOutput}\nSTDERR:\n{standardError}"
            );
        }

        return standardOutput.Trim();
    }

    private string RunAllowFailure(string fileName, params string[] arguments)
    {
        var (_, standardOutput, _) = RunProcess(fileName, arguments);
        return standardOutput.Trim();
    }

    private (int ExitCode, string StandardOutput, string StandardError) RunProcess(
        string fileName,
        string[] arguments,
        string? pathPrefix = null
    )
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = _rootDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (pathPrefix is not null)
        {
            process.StartInfo.Environment["PATH"] =
                pathPrefix + Path.PathSeparator + process.StartInfo.Environment["PATH"];
        }

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, standardOutput, standardError);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "versioning-samples.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root for versioning-samples."
        );
    }
}
