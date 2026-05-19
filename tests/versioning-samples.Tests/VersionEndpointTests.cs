using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using versioning_samples;
using Xunit;

namespace versioning_samples.Tests;

public class VersionEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Version_endpoint_returns_version_information()
    {
        using var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<AppVersionInfo>("/version");

        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.SemVer));
        Assert.False(string.IsNullOrWhiteSpace(payload.InformationalVersion));
        Assert.False(string.IsNullOrWhiteSpace(payload.Source));
    }
}
