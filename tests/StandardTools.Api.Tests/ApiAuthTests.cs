using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StandardTools.Api.Tests;

[Collection("Api")]
public class ApiAuthTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiAuthTests()
    {
        Environment.SetEnvironmentVariable("SQT_AUTH_ENABLED", "true");
        Environment.SetEnvironmentVariable("SQT_API_KEY", "test-secret");
        _factory = new WebApplicationFactory<Program>();
    }

    public void Dispose()
    {
        _factory.Dispose();
        Environment.SetEnvironmentVariable("SQT_AUTH_ENABLED", "false");
        Environment.SetEnvironmentVariable("SQT_API_KEY", null);
    }

    [Fact]
    public async Task MissingKey_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/agent/tools");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongKey_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", "wrong-key");
        var response = await client.GetAsync("/api/v1/agent/tools");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CorrectKey_Returns200()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", "test-secret");
        var response = await client.GetAsync("/api/v1/agent/tools");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Health_DoesNotRequireKey()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
    }
}
