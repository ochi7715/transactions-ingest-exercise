using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Transactions.Api.Tests;

public class ApiTests
{
    [Fact]
    public async Task EmptyBodyUsesBaselineScenario()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/api/import", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("baseline", result.GetProperty("scenario").GetString());
    }

    [Fact]
    public async Task RestartDoesNotChangeMockTransactionTimes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"restart-{Guid.NewGuid():N}.db");
        try
        {
            using (var first = new ApiFactory(path))
            using (var client = first.CreateClient())
                (await client.PostAsJsonAsync("/api/import", new { scenario = "baseline" })).EnsureSuccessStatusCode();
            using (var second = new ApiFactory(path))
            using (var client = second.CreateClient())
            {
                var response = await client.PostAsJsonAsync("/api/import", new { scenario = "baseline" });
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                Assert.All(result.GetProperty("sources").EnumerateArray(), x =>
                {
                    Assert.Equal(0, x.GetProperty("updated").GetInt32());
                    Assert.Equal(3, x.GetProperty("unchanged").GetInt32());
                });
            }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task EndpointsImportReadAndAuditUsingMigratedDatabase()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/transactions"))!);
        var response = await client.PostAsJsonAsync("/api/import", new { scenario = "baseline" });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Succeeded", result.GetProperty("status").GetString());
        var rows = (await client.GetFromJsonAsync<JsonElement[]>("/api/transactions"))!;
        Assert.Equal(9, rows.Length);
        Assert.EndsWith("Z", rows[0].GetProperty("transactionTime").GetString());
        var audit = await client.GetFromJsonAsync<JsonElement[]>($"/api/transactions/{rows[0].GetProperty("id")}/audit");
        Assert.Single(audit!);
        var repeat = await client.PostAsJsonAsync("/api/import", new { scenario = "baseline" });
        repeat.EnsureSuccessStatusCode();
        var repeated = await repeat.Content.ReadFromJsonAsync<JsonElement>();
        Assert.All(repeated.GetProperty("sources").EnumerateArray(), x => Assert.Equal(3, x.GetProperty("unchanged").GetInt32()));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/transactions/999999/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/import", new { scenario = "invalid" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/import", new StringContent("{broken", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(2, (await client.GetFromJsonAsync<JsonElement[]>("/api/import/runs"))!.Length);
    }

    [Fact]
    public async Task DemoSequenceProducesCorrectionsRevocationsAndRestorations()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        async Task<JsonElement> Import(string scenario)
        {
            var response = await client.PostAsJsonAsync("/api/import", new { scenario });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }
        await Import("baseline");
        var corrected = await Import("corrected");
        Assert.All(corrected.GetProperty("sources").EnumerateArray(), x =>
        {
            Assert.Equal(1, x.GetProperty("inserted").GetInt32());
            Assert.Equal(1, x.GetProperty("updated").GetInt32());
        });
        var missing = await Import("missing");
        Assert.Equal(1, missing.GetProperty("sources")[0].GetProperty("revoked").GetInt32());
        var restored = await Import("restored");
        Assert.Equal(1, restored.GetProperty("sources")[0].GetProperty("restored").GetInt32());
        Assert.Equal("PartiallySucceeded", (await Import("failure")).GetProperty("status").GetString());
        Assert.Equal("Failed", (await Import("incomplete")).GetProperty("status").GetString());
    }
}

internal sealed class ApiFactory(string? existingDatabasePath = null) : WebApplicationFactory<Program>
{
    private readonly string databasePath = existingDatabasePath ?? Path.Combine(Path.GetTempPath(), $"transactions-{Guid.NewGuid():N}.db");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Transactions"] = $"Data Source={databasePath};Pooling=False" }));
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && existingDatabasePath is null && File.Exists(databasePath)) File.Delete(databasePath);
    }
}
