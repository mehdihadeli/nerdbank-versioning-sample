using Xunit;

namespace versioning_samples.Tests;

public sealed class VersioningStrategyTests
{
    [Fact]
    public void Customer_export_authentication_and_release_tags_follow_version_scenario()
    {
        using var sandbox = VersioningSandbox.Create();

        sandbox.CreateBranch("feature/customer-export");
        sandbox.CommitChange("feat: add customer export");
        sandbox.MergeSquashToMain("feature/customer-export");
        var firstPreview = sandbox.CalculateSemanticVersion();
        Assert.Equal("1.0.0-preview.1", firstPreview);

        sandbox.CreateBranch("feature/add-auth");
        sandbox.CommitChange("feat: add authentication");
        sandbox.MergeSquashToMain("feature/add-auth");
        var secondPreview = sandbox.CalculateSemanticVersion();
        Assert.Equal("1.0.0-preview.2", secondPreview);
        Assert.NotEqual(firstPreview, secondPreview);

        // The RC transition is prepared on a branch and merged into protected main.
        sandbox.CreateBranch("chore/prepare-1.0.0-rc");
        sandbox.PrepareStaging("1.0.0");
        sandbox.MergeSquashToMain("chore/prepare-1.0.0-rc");
        sandbox.CreateNbgvTag();
        var firstCandidate = sandbox.CalculateSemanticVersion();
        Assert.Equal("1.0.0-rc.1", firstCandidate);
        Assert.Equal("v1.0.0-rc.1", sandbox.GetTagPointingAtHead("^v1\\.0\\.0-rc\\.1$"));

        // RC fixes keep the rc.{height} policy from version.json. The new commit height
        // produces a new RC value; no manual RC counter or version-file edit is needed.
        sandbox.CommitChange("fix: correct release candidate behavior");
        sandbox.CreateNbgvTag();
        var secondCandidate = sandbox.CalculateSemanticVersion();
        Assert.Equal("1.0.0-rc.2", secondCandidate);
        Assert.Equal("v1.0.0-rc.2", sandbox.GetTagPointingAtHead("^v1\\.0\\.0-rc\\.2$"));

        sandbox.CreateBranch("chore/prepare-1.0.0");
        sandbox.PrepareProduction("1.0.0");
        sandbox.MergeSquashToMain("chore/prepare-1.0.0");
        sandbox.CreateNbgvTag();
        Assert.Equal("1.0.0", sandbox.CalculateSemanticVersion());
        Assert.Equal("v1.0.0", sandbox.GetTagPointingAtHead("^v1\\.0\\.0$"));

        // Prepare the next train on a branch before merging into protected main.
        sandbox.CreateBranch("chore/prepare-1.1.0-preview");
        sandbox.PrepareNextPreviewTrain("1.1.0");
        sandbox.MergeSquashToMain("chore/prepare-1.1.0-preview");
        sandbox.CreateBranch("feature/add-authorization");
        sandbox.CommitChange("feat: add authorization");
        sandbox.MergeSquashToMain("feature/add-authorization");
        var nextPreview = sandbox.CalculateSemanticVersion();
        Assert.Equal("1.1.0-preview.1", nextPreview);
        AssertPreview(nextPreview);

        sandbox.CreateBranch("feature/add-reporting");
        sandbox.CommitChange("feat: add reporting");
        sandbox.MergeSquashToMain("feature/add-reporting");
        var nextPreviewTwo = sandbox.CalculateSemanticVersion();
        Assert.Equal("1.1.0-preview.2", nextPreviewTwo);
        Assert.NotEqual(nextPreview, nextPreviewTwo);
    }

    [Fact]
    public void Manual_tag_does_not_override_version_calculated_from_version_json()
    {
        using var sandbox = VersioningSandbox.Create();

        sandbox.CreateBranch("feature/customer-export");
        sandbox.CommitChange("feat: add customer export");
        sandbox.MergeSquashToMain("feature/customer-export");
        sandbox.CreateTag("v9.9.9");

        Assert.Equal("1.0.0-preview.1", sandbox.CalculateSemanticVersion());
        Assert.Equal("v9.9.9", sandbox.GetTagPointingAtHead("^v9\\.9\\.9$"));
    }

    [Fact]
    public void Customer_export_authentication_and_release_tags_follow_helper_scenario()
    {
        using var sandbox = VersioningSandbox.Create();

        sandbox.CreateBranch("feature/customer-export");
        sandbox.CommitChange("feat: add customer export");
        sandbox.MergeSquashToMain("feature/customer-export");
        Assert.Equal("1.0.0-preview.1", sandbox.CalculateSemanticVersion());

        sandbox.CreateBranch("feature/add-auth");
        sandbox.CommitChange("feat: add authentication");
        sandbox.MergeSquashToMain("feature/add-auth");
        Assert.Equal("1.0.0-preview.2", sandbox.CalculateSemanticVersion());

        sandbox.CreateBranch("chore/prepare-1.0.0-rc");
        sandbox.RunReleaseVersionScript("prepare-rc", "1.0.0");
        sandbox.CommitVersionChange("chore: prepare 1.0.0 RC train");
        sandbox.MergeSquashToMain("chore/prepare-1.0.0-rc");
        sandbox.RunReleaseVersionScript("tag");
        Assert.Equal("1.0.0-rc.1", sandbox.CalculateSemanticVersion());
        Assert.Equal("v1.0.0-rc.1", sandbox.GetTagPointingAtHead("^v1\\.0\\.0-rc\\.1$"));

        sandbox.CreateBranch("fix/release-candidate");
        sandbox.CommitChange("fix: correct release candidate behavior");
        sandbox.MergeSquashToMain("fix/release-candidate");
        sandbox.RunReleaseVersionScript("tag");
        Assert.Equal("1.0.0-rc.2", sandbox.CalculateSemanticVersion());
        Assert.Equal("v1.0.0-rc.2", sandbox.GetTagPointingAtHead("^v1\\.0\\.0-rc\\.2$"));

        sandbox.CreateBranch("chore/prepare-1.0.0");
        sandbox.RunReleaseVersionScript("prepare-stable", "1.0.0");
        sandbox.CommitVersionChange("chore: prepare 1.0.0");
        sandbox.MergeSquashToMain("chore/prepare-1.0.0");
        sandbox.RunReleaseVersionScript("tag");
        Assert.Equal("1.0.0", sandbox.CalculateSemanticVersion());
        Assert.Equal("v1.0.0", sandbox.GetTagPointingAtHead("^v1\\.0\\.0$"));

        sandbox.CreateBranch("chore/prepare-1.1.0-preview");
        sandbox.RunReleaseVersionScript("prepare-train", "1.1.0");
        sandbox.CommitVersionChange("chore: start 1.1.0 preview train");
        sandbox.MergeSquashToMain("chore/prepare-1.1.0-preview");

        sandbox.CreateBranch("feature/add-authorization");
        sandbox.CommitChange("feat: add authorization");
        sandbox.MergeSquashToMain("feature/add-authorization");
        Assert.Equal("1.1.0-preview.1", sandbox.CalculateSemanticVersion());

        sandbox.CreateBranch("feature/add-reporting");
        sandbox.CommitChange("feat: add reporting");
        sandbox.MergeSquashToMain("feature/add-reporting");
        Assert.Equal("1.1.0-preview.2", sandbox.CalculateSemanticVersion());
    }

    private static void AssertPreview(string version)
    {
        Assert.Matches(
            "^[0-9]+\\.[0-9]+\\.[0-9]+-preview\\.[0-9]+(?:\\.g[0-9A-Za-z]+|\\+[0-9A-Za-z.-]+)?$",
            version
        );
    }
}
