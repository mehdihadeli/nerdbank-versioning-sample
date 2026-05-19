using Xunit;

namespace versioning_samples.Tests;

public sealed class VersioningStrategyTests
{
    [Fact]
    public void Preview_builds_on_main_use_base_and_effective_dev_versions()
    {
        using var sandbox = VersioningSandbox.Create();

        AssertPreviewState(sandbox, "1.0.0-preview.1", expectedRevision: 1);

        sandbox.CreateBranch("feat/login");
        sandbox.CommitChange("feat(auth): add authentication");
        sandbox.MergeSquashToMain("feat/login");
        AssertPreviewState(sandbox, "1.0.0-preview.2", expectedRevision: 2);

        sandbox.CreateBranch("fix/login-bug");
        sandbox.CommitChange("fix(auth): resolve token issue");
        sandbox.MergeSquashToMain("fix/login-bug");
        AssertPreviewState(sandbox, "1.0.0-preview.3", expectedRevision: 3);
    }

    [Fact]
    public void Release_candidate_and_production_promotions_match_local_nbgv_versions()
    {
        using var sandbox = VersioningSandbox.Create();

        sandbox.CreateBranch("feat/login");
        sandbox.CommitChange("feat(auth): add authentication");
        sandbox.MergeSquashToMain("feat/login");

        sandbox.CreateBranch("fix/login-bug");
        sandbox.CommitChange("fix(auth): resolve token issue");
        sandbox.MergeSquashToMain("fix/login-bug");

        sandbox.PrepareStaging("1.0.0", 1);
        Assert.Equal("1.0.0-rc.1", sandbox.GetSemVer2());
        sandbox.CreateTag("v1.0.0-rc.1", "Release candidate 1");
        Assert.Equal("v1.0.0-rc.1", sandbox.GetTagPointingAtHead("^v1\\.0\\.0-rc\\.1$"));

        sandbox.CreateBranch("fix/rc-bug");
        sandbox.CommitChange("fix(auth): resolve RC issue");
        sandbox.MergeSquashToMain("fix/rc-bug");
        Assert.Equal("1.0.0-rc.1", sandbox.GetSemVer2());

        sandbox.PrepareStaging("1.0.0", 2);
        Assert.Equal("1.0.0-rc.2", sandbox.GetSemVer2());
        sandbox.CreateTag("v1.0.0-rc.2", "Release candidate 2");
        Assert.Equal("v1.0.0-rc.2", sandbox.GetTagPointingAtHead("^v1\\.0\\.0-rc\\.2$"));

        sandbox.PrepareProduction("1.0.0");
        Assert.Equal("1.0.0", sandbox.GetSemVer2());
        sandbox.CreateTag("v1.0.0", "Production release");
        Assert.Equal("v1.0.0", sandbox.GetTagPointingAtHead("^v1\\.0\\.0$"));
    }

    [Fact]
    public void End_to_end_strategy_transitions_preview_rc_stable_and_next_preview_train()
    {
        using var sandbox = VersioningSandbox.Create();

        AssertPreviewState(sandbox, "1.0.0-preview.1", expectedRevision: 1);

        sandbox.CreateBranch("feat/login");
        sandbox.CommitChange("feat(auth): add authentication");
        sandbox.MergeSquashToMain("feat/login");
        AssertPreviewState(sandbox, "1.0.0-preview.2", expectedRevision: 2);

        sandbox.CreateBranch("fix/login-bug");
        sandbox.CommitChange("fix(auth): resolve token issue");
        sandbox.MergeSquashToMain("fix/login-bug");
        AssertPreviewState(sandbox, "1.0.0-preview.3", expectedRevision: 3);

        sandbox.PrepareStaging("1.0.0", 1);
        Assert.Equal("1.0.0-rc.1", sandbox.GetSemVer2());
        sandbox.CreateTag("v1.0.0-rc.1", "Release candidate 1");
        Assert.Equal("v1.0.0-rc.1", sandbox.GetTagPointingAtHead("^v1\\.0\\.0-rc\\.1$"));

        sandbox.CreateBranch("fix/rc-bug");
        sandbox.CommitChange("fix(auth): resolve release candidate regression");
        sandbox.MergeSquashToMain("fix/rc-bug");
        Assert.Equal("1.0.0-rc.1", sandbox.GetSemVer2());

        sandbox.PrepareStaging("1.0.0", 2);
        Assert.Equal("1.0.0-rc.2", sandbox.GetSemVer2());
        sandbox.CreateTag("v1.0.0-rc.2", "Release candidate 2");
        Assert.Equal("v1.0.0-rc.2", sandbox.GetTagPointingAtHead("^v1\\.0\\.0-rc\\.2$"));

        sandbox.PrepareProduction("1.0.0");
        Assert.Equal("1.0.0", sandbox.GetSemVer2());
        sandbox.CreateTag("v1.0.0", "Production release");
        Assert.Equal("v1.0.0", sandbox.GetTagPointingAtHead("^v1\\.0\\.0$"));

        sandbox.PrepareNextPreviewTrain("1.1.0");
        AssertPreviewState(sandbox, "1.1.0-preview.1", expectedRevision: 8);
    }

    private static void AssertPreviewState(
        VersioningSandbox sandbox,
        string expectedBaseSemVer,
        int expectedRevision
    )
    {
        var actualBaseSemVer = sandbox.GetSemVer2();
        var effectiveDevVersion = sandbox.GetExpectedDevVersion(actualBaseSemVer);
        var expectedDockerTag = sandbox.GetExpectedDockerTag(effectiveDevVersion);

        Assert.Equal(expectedBaseSemVer, actualBaseSemVer);
        Assert.Equal(expectedRevision, sandbox.GetRevision());
        Assert.Equal(
            $"{expectedBaseSemVer}.{sandbox.GetDateStampUtc()}.{expectedRevision}+{sandbox.GetCommitShort()}",
            effectiveDevVersion
        );
        Assert.Equal(effectiveDevVersion.Replace('+', '-'), expectedDockerTag);
        Assert.Contains("preview", sandbox.GetAssemblyInformationalVersion());
    }
}
