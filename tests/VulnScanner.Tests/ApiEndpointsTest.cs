using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Models;
using Api.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace VulnScanner.Tests;

public sealed class ApiEndpointsTests
{
    [Fact]
    public async Task CreateVulnerability_ReturnsId_AndCanBeRetrieved()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/v1/vulnerabilities",
            new
            {
                name = "Test vulnerability",
                severity = "High"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Single(created.EnumerateObject());

        var id = created.GetProperty("id").GetGuid();

        Assert.NotEqual(Guid.Empty, id);

        var getResponse = await client.GetAsync(
            response.Headers.Location);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var vulnerability =
            await getResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            id,
            vulnerability.GetProperty("id").GetGuid());

        Assert.Equal(
            "Test vulnerability",
            vulnerability.GetProperty("name").GetString());

        Assert.Equal(
            "High",
            vulnerability.GetProperty("severity").GetString());

        Assert.NotEqual(
            default(DateTimeOffset),
            vulnerability.GetProperty("createdAt").GetDateTimeOffset());
    }

    [Theory]
    [InlineData("", "High")]
    [InlineData("Test vulnerability", "InvalidSeverity")]
    public async Task InvalidVulnerability_Returns400(
        string name,
        string severity)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/v1/vulnerabilities",
            new { name, severity });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Theory]
    [InlineData("/v1/vulnerabilities/")]
    [InlineData("/v1/scans/")]
    public async Task UnknownResource_Returns404(string route)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            route + Guid.NewGuid());

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateScan_Returns202_AndQueuedStatus()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var id = await CreateScanAsync(client);

        var response = await client.GetAsync(
            $"/v1/scans/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var scan =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            id,
            scan.GetProperty("id").GetGuid());

        Assert.Equal(
            "Queued",
            scan.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("https://example.org/project.git")]
    [InlineData("http://github.com/example/project.git")]
    public async Task InvalidRepository_Returns400(string url)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/v1/scans",
            new { repositoryUrl = url });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CancelQueuedScan_ReturnsConfirmation()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var id = await CreateScanAsync(client);

        var response = await client.PostAsync(
            $"/v1/scans/{id}/cancel",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cancellation =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            id,
            cancellation.GetProperty("id").GetGuid());

        Assert.True(
            cancellation.GetProperty("cancellationRequested")
                .GetBoolean());

        Assert.NotEqual(
            default(DateTimeOffset),
            cancellation.GetProperty("requestedAt")
                .GetDateTimeOffset());

        var scan = await client.GetFromJsonAsync<JsonElement>(
            $"/v1/scans/{id}");

        Assert.Equal(
            "Cancelled",
            scan.GetProperty("status").GetString());
    }

    [Fact]
    public async Task CancelUnknownScan_Returns404()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(
            $"/v1/scans/{Guid.NewGuid()}/cancel",
            content: null);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task CancelCompletedScan_Returns409()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var id = await CreateScanAsync(client);
        var repository =
            factory.Services.GetRequiredService<ScanRepository>();

        var job = repository.GetById(id);

        Assert.NotNull(job);

        lock (job.SyncRoot)
        {
            job.Status = ScanStatus.Completed;
            job.LastStatusChangedAt = DateTimeOffset.UtcNow;
        }

        var response = await client.PostAsync(
            $"/v1/scans/{id}/cancel",
            content: null);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task FullQueue_Returns429()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        await CreateScanAsync(client);

        var response = await client.PostAsJsonAsync(
            "/v1/scans",
            new
            {
                repositoryUrl =
                    "https://github.com/example/second.git"
            });

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            response.StatusCode);
    }

    private static async Task<Guid> CreateScanAsync(
        HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/v1/scans",
            new
            {
                repositoryUrl =
                    "https://github.com/example/project.git"
            });

        Assert.Equal(
            HttpStatusCode.Accepted,
            response.StatusCode);

        Assert.NotNull(response.Headers.Location);

        var body =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        return body.GetProperty("id").GetGuid();
    }
}