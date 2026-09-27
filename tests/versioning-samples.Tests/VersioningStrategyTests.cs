using Xunit;

namespace versioning_samples.Tests;

public sealed class VersioningStrategyTests
{
    [Fact]
    public void Customer_export_authentication_and_release_tags_follow_nbgv_scenario()
    {
        using var sandbox = VersioningSandbox.Create();
        Assert.Equal("1.0.0-preview.0", sandbox.CalculateSemanticVersion());

        sandbox.MergePullRequest("feature/customer-export", "feat: add customer export");
        Assert.Equal("1.0.0-preview.1", sandbox.CalculateSemanticVersion());
        sandbox.MergePullRequest("feature/add-auth", "feat: add authentication");
        Assert.Equal("1.0.0-preview.2", sandbox.CalculateSemanticVersion());

        // The RC transition is prepared on a branch and merged into protected main.
        sandbox.PrepareVersionWithNbgv("chore/prepare-1.0.0-rc", "1.0.0-rc.{height}");
        sandbox.MergePullRequest("chore/prepare-1.0.0-rc", "chore: prepare 1.0.0 RC train");
        sandbox.CreateNbgvTag();
        Assert.Equal("1.0.0-rc.1", sandbox.CalculateSemanticVersion());
        Assert.Contains("v1.0.0-rc.1", sandbox.TagsAtHead());

        sandbox.MergePullRequest(
            "fix/release-candidate",
            "fix: correct release candidate behavior"
        );
        Assert.Equal("1.0.0-rc.2", sandbox.CalculateSemanticVersion());
        sandbox.CreateNbgvTag();
        Assert.Equal("1.0.0-rc.2", sandbox.CalculateSemanticVersion());
        Assert.Contains("v1.0.0-rc.2", sandbox.TagsAtHead());

        sandbox.PrepareVersionWithNbgv(
            "chore/prepare-1.0.0",
            "1.0.0",
            removeVersionHeightOffset: true
        );
        sandbox.MergePullRequest("chore/prepare-1.0.0", "chore: prepare 1.0.0");
        sandbox.CreateNbgvTag();
        Assert.Equal("1.0.0", sandbox.CalculateSemanticVersion());
        Assert.Contains("v1.0.0", sandbox.TagsAtHead());

        // Prepare the next train on a branch before merging into protected main.
        sandbox.PrepareVersionWithNbgv(
            "chore/prepare-1.1.0-preview",
            "1.1.0-preview.{height}",
            removeVersionHeightOffset: true
        );
        sandbox.MergePullRequest("chore/prepare-1.1.0-preview", "chore: start 1.1.0 preview train");
        Assert.Equal("1.1.0-preview.1", sandbox.CalculateSemanticVersion());
        sandbox.MergePullRequest("feature/add-authorization", "feat: add authorization");
        Assert.Equal("1.1.0-preview.2", sandbox.CalculateSemanticVersion());
        sandbox.MergePullRequest("feature/add-reporting", "feat: add reporting");
        Assert.Equal("1.1.0-preview.3", sandbox.CalculateSemanticVersion());
    }

    [Fact]
    public void Customer_export_authentication_and_release_tags_follow_release_script_scenario()
    {
        using var sandbox = VersioningSandbox.Create();
        Assert.Equal("1.0.0-preview.0", sandbox.CalculateSemanticVersion());

        sandbox.MergePullRequest("feature/customer-export", "feat: add customer export");
        Assert.Equal("1.0.0-preview.1", sandbox.CalculateSemanticVersion());
        sandbox.MergePullRequest("feature/add-auth", "feat: add authentication");
        Assert.Equal("1.0.0-preview.2", sandbox.CalculateSemanticVersion());

        sandbox.CreateBranch("chore/prepare-1.0.0-rc");
        sandbox.RunReleaseVersionScript("prepare-rc", "1.0.0");
        sandbox.MergePullRequest("chore/prepare-1.0.0-rc", "chore: prepare 1.0.0 RC train");
        sandbox.RunReleaseVersionScript("tag");
        Assert.Equal("1.0.0-rc.1", sandbox.CalculateSemanticVersion());
        Assert.Contains("v1.0.0-rc.1", sandbox.TagsAtHead());

        sandbox.MergePullRequest(
            "fix/release-candidate",
            "fix: correct release candidate behavior"
        );
        sandbox.RunReleaseVersionScript("tag");
        Assert.Equal("1.0.0-rc.2", sandbox.CalculateSemanticVersion());
        Assert.Contains("v1.0.0-rc.2", sandbox.TagsAtHead());

        sandbox.CreateBranch("chore/prepare-1.0.0");
        sandbox.RunReleaseVersionScript("prepare-stable", "1.0.0");
        sandbox.MergePullRequest("chore/prepare-1.0.0", "chore: prepare 1.0.0");
        sandbox.RunReleaseVersionScript("tag");
        Assert.Equal("1.0.0", sandbox.CalculateSemanticVersion());
        Assert.Contains("v1.0.0", sandbox.TagsAtHead());

        sandbox.CreateBranch("chore/prepare-1.1.0-preview");
        sandbox.RunReleaseVersionScript("prepare-train", "1.1.0");
        sandbox.MergePullRequest("chore/prepare-1.1.0-preview", "chore: start 1.1.0 preview train");
        Assert.Equal("1.1.0-preview.1", sandbox.CalculateSemanticVersion());

        sandbox.MergePullRequest("feature/add-authorization", "feat: add authorization");
        Assert.Equal("1.1.0-preview.2", sandbox.CalculateSemanticVersion());
        sandbox.MergePullRequest("feature/add-reporting", "feat: add reporting");
        Assert.Equal("1.1.0-preview.3", sandbox.CalculateSemanticVersion());
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

        Assert.Equal("1.0.0-preview.1", sandbox.CalculateSemanticVersion());
        Assert.Equal("v9.9.9", sandbox.GetTagPointingAtHead("^v9\\.9\\.9$"));
    }
}
