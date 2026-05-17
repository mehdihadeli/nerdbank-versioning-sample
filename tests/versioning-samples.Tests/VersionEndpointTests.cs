using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using versioning_samples;
using Xunit;

namespace versioning_samples.Tests;

public class VersionEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public VersionEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Version_endpoint_returns_version_information()
    {
        using var client = _factory.CreateClient();

        var payload = await client.GetFromJsonAsync<AppVersionInfo>("/version");

        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.SemVer));
        Assert.False(string.IsNullOrWhiteSpace(payload.InformationalVersion));
        Assert.False(string.IsNullOrWhiteSpace(payload.Source));
    }
}
