using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Transactions.Api.Data;
using Transactions.Api.Services;
using Transactions.Api.Sources;

namespace Transactions.Api.Tests;

public class ImportTests
{
    [Fact]
    public async Task RepeatingSnapshotDoesNotChangeTransactionsOrAudit()
    {
        await using var fixture = new Fixture();
        var source = fixture.Source("Online");
        source.Rows = [fixture.Row()];
        var first = await fixture.Import(source);
        var before = await fixture.Db.Transactions.AsNoTracking().SingleAsync();
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(5);
        var second = await fixture.Import(source);
        var after = await fixture.Db.Transactions.AsNoTracking().SingleAsync();
        Assert.Equal(1, first.Sources.Single().Inserted);
        Assert.Equal(1, second.Sources.Single().Unchanged);
        Assert.Equal(1, await fixture.Db.Transactions.CountAsync());
        Assert.Equal(1, await fixture.Db.AuditChanges.CountAsync());
        Assert.Equal(before.UpdatedAtUtc, after.UpdatedAtUtc);
        Assert.Equal(before.RevisionNumber, after.RevisionNumber);
        Assert.Equal(2, await fixture.Db.ImportRuns.CountAsync());
    }

    [Fact]
    public async Task SameIdFromDifferentSourcesCreatesSeparateRecords()
    {
        await using var f = new Fixture();
        var online = f.Source("Online"); online.Rows = [f.Row()];
        var uptown = f.Source("Store Uptown"); uptown.Rows = [f.Row("Store Uptown")];
        await f.Import(online, uptown);
        Assert.Equal(2, await f.Db.Transactions.CountAsync());
    }

    [Fact]
    public async Task ChangesAreAuditedForEveryBusinessField()
    {
        await using var f = new Fixture();
        var source = f.Source("Online"); source.Rows = [f.Row()];
        await f.Import(source);
        source.Rows = [f.Row() with { Amount = 29.99m, ProductName = "New Mouse", CardNumber = "2222",
            LocationCode = "WEB", TransactionTime = f.Clock.Now.AddMinutes(-30) }];
        var result = await f.Import(source);
        var changes = await f.Db.AuditChanges.Where(x => x.EventType == "FieldChanged").ToListAsync();
        Assert.Equal(5, changes.Count);
        var amount = Assert.Single(changes, x => x.FieldName == "Amount");
        Assert.Equal("24.99", amount.OldValue);
        Assert.Equal("29.99", amount.NewValue);
        Assert.Equal(1, result.Sources.Single().Updated);
        Assert.Equal(2, (await f.Db.Transactions.SingleAsync()).RevisionNumber);
    }

    [Theory]
    [InlineData(23, "Revoked")]
    [InlineData(24, "Revoked")]
    [InlineData(25, "Active")]
    public async Task MissingTransactionsRespectAgeBoundary(int hours, string expected)
    {
        await using var f = new Fixture();
        var source = f.Source("Online");
        source.Rows = [f.Row() with { TransactionTime = f.Clock.Now.AddHours(-hours) }];
        await f.Import(source);
        source.Rows = [];
        await f.Import(source);
        await f.Import(source);
        Assert.Equal(expected, (await f.Db.Transactions.SingleAsync()).Status);
        Assert.Equal(expected == "Revoked" ? 1 : 0,
            await f.Db.AuditChanges.CountAsync(x => x.EventType == "Revoked"));
    }

    [Fact]
    public async Task MissingRecordOutsideCoverageIsPreserved()
    {
        await using var f = new Fixture();
        var source = f.Source("Online"); source.Rows = [f.Row()];
        await f.Import(source);
        source.Rows = [];
        source.CoverageStart = f.Clock.Now.AddMinutes(-20);
        await f.Import(source);
        Assert.Equal("Active", (await f.Db.Transactions.SingleAsync()).Status);
    }

    [Fact]
    public async Task ReturningRecordIsRestoredAndRepeatedRestoreIsHarmless()
    {
        await using var f = new Fixture();
        var source = f.Source("Online"); var row = f.Row(); source.Rows = [row];
        await f.Import(source);
        source.Rows = []; await f.Import(source);
        source.Rows = [row]; var restored = await f.Import(source);
        await f.Import(source);
        Assert.Equal(1, restored.Sources.Single().Restored);
        Assert.Equal("Active", (await f.Db.Transactions.SingleAsync()).Status);
        Assert.Equal(1, await f.Db.AuditChanges.CountAsync(x => x.EventType == "Restored"));
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("incomplete")]
    [InlineData("duplicate")]
    [InlineData("source")]
    [InlineData("length")]
    [InlineData("precision")]
    [InlineData("range")]
    [InlineData("future")]
    [InlineData("utc")]
    [InlineData("card")]
    [InlineData("stale")]
    public async Task InvalidOrFailedSnapshotsPreserveExistingData(string issue)
    {
        await using var f = new Fixture();
        var source = f.Source("Online"); var row = f.Row(); source.Rows = [row];
        await f.Import(source);
        source.Rows = issue switch
        {
            "duplicate" => [row, row],
            "source" => [row with { SourceSystem = "Store Uptown" }],
            "length" => [row with { ProductName = new string('x', 20) }],
            "precision" => [row with { Amount = 1.001m }],
            "range" => [row with { Amount = 10000000000000000m }],
            "future" => [row with { TransactionTime = f.Clock.Now.AddMinutes(1) }],
            "utc" => [row with { TransactionTime = DateTime.SpecifyKind(row.TransactionTime, DateTimeKind.Unspecified) }],
            "card" => [row with { CardNumber = "1234567890123456" }],
            _ => []
        };
        source.Fail = issue == "failure";
        source.Complete = issue != "incomplete";
        source.Stale = issue == "stale";
        var result = await f.Import(source);
        Assert.Equal("Failed", result.Status);
        var stored = await f.Db.Transactions.SingleAsync();
        Assert.Equal("Active", stored.Status);
        Assert.Equal(2499, stored.AmountInCents);
        Assert.Equal(1, await f.Db.AuditChanges.CountAsync());
    }

    [Fact]
    public async Task FailureInOneSourceDoesNotPreventAnotherSourceImport()
    {
        await using var f = new Fixture();
        var online = f.Source("Online"); online.Fail = true;
        var uptown = f.Source("Store Uptown"); uptown.Rows = [f.Row("Store Uptown")];
        var result = await f.Import(online, uptown);
        Assert.Equal("PartiallySucceeded", result.Status);
        Assert.Equal("Store Uptown", (await f.Db.Transactions.SingleAsync()).SourceSystem);
    }

    [Fact]
    public async Task SourceCannotRevokeAnotherSourcesTransactions()
    {
        await using var f = new Fixture();
        var online = f.Source("Online"); online.Rows = [f.Row()];
        var uptown = f.Source("Store Uptown"); uptown.Rows = [f.Row("Store Uptown")];
        await f.Import(online, uptown);
        uptown.Rows = [];
        await f.Import(uptown);
        Assert.Equal("Active", (await f.Db.Transactions.SingleAsync(x => x.SourceSystem == "Online")).Status);
        Assert.Equal("Revoked", (await f.Db.Transactions.SingleAsync(x => x.SourceSystem == "Store Uptown")).Status);
    }

    [Fact]
    public async Task AuditFailureRollsBackTransactionChanges()
    {
        await using var f = new Fixture();
        var source = f.Source("Online"); source.Rows = [f.Row()];
        await f.Import(source);
        await f.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectAudit BEFORE INSERT ON AuditChanges BEGIN SELECT RAISE(ABORT, 'Audit unavailable'); END;");
        source.Rows = [f.Row() with { Amount = 50m }, f.Row() with { TransactionId = 2 }];
        var result = await f.Import(source);
        Assert.Equal("Failed", result.Status);
        Assert.Equal(1, await f.Db.Transactions.CountAsync());
        Assert.Equal(2499, (await f.Db.Transactions.SingleAsync()).AmountInCents);
        Assert.Equal(1, await f.Db.AuditChanges.CountAsync());
    }

    [Fact]
    public async Task DatabaseEnforcesCombinedIdentity()
    {
        await using var f = new Fixture();
        var source = f.Source("Online"); source.Rows = [f.Row()]; await f.Import(source);
        await Assert.ThrowsAsync<SqliteException>(() => f.Db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Transactions (SourceSystem,TransactionId,CardNumber,LocationCode,ProductName,AmountInCents,TransactionTime,Status,RevisionNumber,CreatedAtUtc,UpdatedAtUtc) SELECT SourceSystem,TransactionId,CardNumber,LocationCode,ProductName,AmountInCents,TransactionTime,Status,RevisionNumber,CreatedAtUtc,UpdatedAtUtc FROM Transactions;"));
    }

    [Fact]
    public async Task ConcurrentImportIsRejected()
    {
        await using var f = new Fixture();
        await f.Gate.Semaphore.WaitAsync();
        try { await Assert.ThrowsAsync<ImportAlreadyRunningException>(() => f.Import(f.Source("Online"))); }
        finally { f.Gate.Semaphore.Release(); }
        Assert.Equal(0, await f.Db.ImportRuns.CountAsync());
    }

    [Fact]
    public async Task RefundAmountsAreStoredExactly()
    {
        await using var f = new Fixture();
        var source = f.Source("Online"); source.Rows = [f.Row() with { Amount = -0.29m }];
        await f.Import(source);
        Assert.Equal(-29, (await f.Db.Transactions.SingleAsync()).AmountInCents);
    }
}

internal sealed class FixedClock : TimeProvider
{
    public DateTime Now { get; set; } = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
    public override DateTimeOffset GetUtcNow() => new(Now);
}

internal sealed class TestSource(string name, FixedClock clock) : ITransactionSource
{
    public string SourceSystem => name;
    public List<SourceTransaction> Rows { get; set; } = [];
    public bool Fail { get; set; }
    public bool Complete { get; set; } = true;
    public bool Stale { get; set; }
    public DateTime CoverageStart { get; set; } = DateTime.UnixEpoch;
    public Task<TransactionSnapshot> GetSnapshotAsync(string scenario, CancellationToken cancellationToken)
    {
        if (Fail) throw new HttpRequestException("Unavailable");
        return Task.FromResult(new TransactionSnapshot(name, Complete, CoverageStart,
            Stale ? clock.Now.AddMinutes(-1) : clock.Now, Rows));
    }
}

internal sealed class Fixture : IAsyncDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public TransactionsDbContext Db { get; }
    public FixedClock Clock { get; } = new();
    public ImportGate Gate { get; } = new();
    public Fixture()
    {
        connection.Open();
        Db = new(new DbContextOptionsBuilder<TransactionsDbContext>().UseSqlite(connection).Options);
        Db.Database.Migrate();
    }
    public TestSource Source(string source) => new(source, Clock);
    public SourceTransaction Row(string source = "Online") =>
        new(source, 1, "1111", "ONLINE", "Mouse", 24.99m, Clock.Now.AddHours(-1));
    public Task<ImportSummary> Import(params ITransactionSource[] sources) =>
        new ImportService(Db, sources, Clock, Gate, Options.Create(new ImportOptions()),
            NullLogger<ImportService>.Instance).ImportAsync("baseline");
    public async ValueTask DisposeAsync()
    {
        Gate.Dispose();
        await Db.DisposeAsync();
        await connection.DisposeAsync();
    }
}
