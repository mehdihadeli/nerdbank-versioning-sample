using Xunit;

namespace versioning_samples.Tests;

public sealed class VersioningStrategyTests
{
    private static void AssertLocalVersion(VersioningSandbox sandbox, string expectedVersion)
    {
        Assert.Equal(expectedVersion, sandbox.CalculateSemanticVersion());
        Assert.Equal(expectedVersion, sandbox.CalculateSemanticVersionWithNbgv());
    }

    [Fact]
    public void Customer_export_authentication_and_release_tags_follow_nbgv_scenario()
    {
        using var sandbox = VersioningSandbox.Create();
        AssertLocalVersion(sandbox, "1.0.0-preview.0");

        sandbox.MergePullRequest("feature/customer-export", "feat: add customer export");
        AssertLocalVersion(sandbox, "1.0.0-preview.1");
        sandbox.MergePullRequest("feature/add-auth", "feat: add authentication");
        AssertLocalVersion(sandbox, "1.0.0-preview.2");

        // The RC transition is prepared on a branch and merged into protected main.
        sandbox.PrepareVersionWithNbgv("chore/prepare-1.0.0-rc", "1.0.0-rc.{height}");
        sandbox.MergePullRequest("chore/prepare-1.0.0-rc", "chore: prepare 1.0.0 RC train");
        sandbox.CreateNbgvTag();
        AssertLocalVersion(sandbox, "1.0.0-rc.1");
        Assert.Contains("v1.0.0-rc.1", sandbox.TagsAtHead());

        sandbox.MergePullRequest(
            "fix/release-candidate",
            "fix: correct release candidate behavior"
        );
        AssertLocalVersion(sandbox, "1.0.0-rc.2");
        sandbox.CreateNbgvTag();
        AssertLocalVersion(sandbox, "1.0.0-rc.2");
        Assert.Contains("v1.0.0-rc.2", sandbox.TagsAtHead());

        sandbox.PrepareVersionWithNbgv(
            "chore/prepare-1.0.0",
            "1.0.0",
            removeVersionHeightOffset: true
        );
        sandbox.MergePullRequest("chore/prepare-1.0.0", "chore: prepare 1.0.0");
        sandbox.CreateNbgvTag();
        AssertLocalVersion(sandbox, "1.0.0");
        Assert.Contains("v1.0.0", sandbox.TagsAtHead());

        // Prepare the next train on a branch before merging into protected main.
        sandbox.PrepareVersionWithNbgv(
            "chore/prepare-1.1.0-preview",
            "1.1.0-preview.{height}",
            removeVersionHeightOffset: true
        );
        sandbox.MergePullRequest("chore/prepare-1.1.0-preview", "chore: start 1.1.0 preview train");
        AssertLocalVersion(sandbox, "1.1.0-preview.1");
        sandbox.MergePullRequest("feature/add-authorization", "feat: add authorization");
        AssertLocalVersion(sandbox, "1.1.0-preview.2");
        sandbox.MergePullRequest("feature/add-reporting", "feat: add reporting");
        AssertLocalVersion(sandbox, "1.1.0-preview.3");
    }

    [Fact]
    public void Customer_export_authentication_and_release_tags_follow_release_script_scenario()
    {
        using var sandbox = VersioningSandbox.Create();
        AssertLocalVersion(sandbox, "1.0.0-preview.0");

        sandbox.MergePullRequest("feature/customer-export", "feat: add customer export");
        AssertLocalVersion(sandbox, "1.0.0-preview.1");
        sandbox.MergePullRequest("feature/add-auth", "feat: add authentication");
        AssertLocalVersion(sandbox, "1.0.0-preview.2");

        sandbox.CreateBranch("chore/prepare-1.0.0-rc");
        sandbox.RunReleaseVersionScript("prepare-rc", "1.0.0");
        sandbox.MergePullRequest("chore/prepare-1.0.0-rc", "chore: prepare 1.0.0 RC train");
        sandbox.RunReleaseVersionScript("tag");
        AssertLocalVersion(sandbox, "1.0.0-rc.1");
        Assert.Contains("v1.0.0-rc.1", sandbox.TagsAtHead());

        sandbox.MergePullRequest(
            "fix/release-candidate",
            "fix: correct release candidate behavior"
        );
        sandbox.RunReleaseVersionScript("tag");
        AssertLocalVersion(sandbox, "1.0.0-rc.2");
        Assert.Contains("v1.0.0-rc.2", sandbox.TagsAtHead());

        sandbox.CreateBranch("chore/prepare-1.0.0");
        sandbox.RunReleaseVersionScript("prepare-stable", "1.0.0");
        sandbox.MergePullRequest("chore/prepare-1.0.0", "chore: prepare 1.0.0");
        sandbox.RunReleaseVersionScript("tag");
        AssertLocalVersion(sandbox, "1.0.0");
        Assert.Contains("v1.0.0", sandbox.TagsAtHead());

        sandbox.CreateBranch("chore/prepare-1.1.0-preview");
        sandbox.RunReleaseVersionScript("prepare-train", "1.1.0");
        sandbox.MergePullRequest("chore/prepare-1.1.0-preview", "chore: start 1.1.0 preview train");
        AssertLocalVersion(sandbox, "1.1.0-preview.1");

        sandbox.MergePullRequest("feature/add-authorization", "feat: add authorization");
        AssertLocalVersion(sandbox, "1.1.0-preview.2");
        sandbox.MergePullRequest("feature/add-reporting", "feat: add reporting");
        AssertLocalVersion(sandbox, "1.1.0-preview.3");
    }

    [Fact]
    public void Manual_tag_does_not_override_version_calculated_from_version_json()
    {
        using var sandbox = VersioningSandbox.Create();
        Assert.Equal("1.0.0-preview.0", sandbox.CalculateSemanticVersion());

        sandbox.CreateBranch("feature/customer-export");
        sandbox.CommitChange("feat: add customer export");
        sandbox.MergeSquashToMain("feature/customer-export");
        sandbox.CreateTag("v9.9.9");

        AssertLocalVersion(sandbox, "1.0.0-preview.1");
        Assert.Equal("v9.9.9", sandbox.GetTagPointingAtHead("^v9\\.9\\.9$"));
    }
}
