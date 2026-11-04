using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Transactions.Api.Data;
using Transactions.Api.Models;
using Transactions.Api.Sources;

namespace Transactions.Api.Services;

public class ImportOptions
{
    public int RevocationHours { get; set; } = 24;
}

public sealed class ImportGate : IDisposable
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
    public void Dispose() => Semaphore.Dispose();
}

public sealed class ImportAlreadyRunningException : Exception;
public record ImportSummary(long Id, DateTime StartedAtUtc, DateTime? CompletedAtUtc,
    string Scenario, string Status, IReadOnlyList<ImportSourceResult> Sources);

public class ImportService(TransactionsDbContext db, IEnumerable<ITransactionSource> sources,
    TimeProvider clock, ImportGate gate, IOptions<ImportOptions> options, ILogger<ImportService> logger)
{
    public async Task<ImportSummary> ImportAsync(string scenario = "baseline", CancellationToken cancellationToken = default)
    {
        if (!DemoScenarios.All.Contains(scenario)) throw new ArgumentException("Unknown demo scenario.");
        if (!await gate.Semaphore.WaitAsync(0, cancellationToken)) throw new ImportAlreadyRunningException();
        try
        {
            var run = new ImportRun { StartedAtUtc = clock.GetUtcNow().UtcDateTime, Scenario = scenario };
            db.ImportRuns.Add(run);
            await db.SaveChangesAsync(cancellationToken);
            var results = new List<ImportSourceResult>();
            try
            {
                foreach (var source in sources)
                {
                    ImportSourceResult result;
                    try
                    {
                        var snapshot = await source.GetSnapshotAsync(scenario, cancellationToken);
                        Validate(source.SourceSystem, snapshot, run.StartedAtUtc);
                        result = await ReconcileAsync(run.Id, run.StartedAtUtc, snapshot, cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        db.ChangeTracker.Clear();
                        logger.LogError(ex, "Import {RunId} failed for {Source}", run.Id, source.SourceSystem);
                        result = new ImportSourceResult
                        {
                            ImportRunId = run.Id, SourceSystem = source.SourceSystem, Status = "Failed",
                            Error = ex is InvalidDataException ? ex.Message : "Source import failed. See server logs."
                        };
                        db.ImportSourceResults.Add(result);
                        await db.SaveChangesAsync(cancellationToken);
                    }
                    results.Add(result);
                    logger.LogInformation("Import {RunId} {Source}: {Status}; inserted {Inserted}, updated {Updated}, revoked {Revoked}, restored {Restored}",
                        run.Id, source.SourceSystem, result.Status, result.Inserted, result.Updated, result.Revoked, result.Restored);
                }
                var failed = results.Count(x => x.Status == "Failed");
                run.Status = failed == 0 ? "Succeeded" : failed == results.Count ? "Failed" : "PartiallySucceeded";
            }
            catch (OperationCanceledException)
            {
                db.ChangeTracker.Clear();
                run.Status = "Cancelled";
                run.CompletedAtUtc = clock.GetUtcNow().UtcDateTime;
                db.ImportRuns.Update(run);
                await db.SaveChangesAsync(CancellationToken.None);
                throw;
            }
            run.CompletedAtUtc = clock.GetUtcNow().UtcDateTime;
            db.ImportRuns.Update(run);
            await db.SaveChangesAsync(cancellationToken);
            return new(run.Id, run.StartedAtUtc, run.CompletedAtUtc, run.Scenario, run.Status, results);
        }
        finally { gate.Semaphore.Release(); }
    }

    private static void Validate(string source, TransactionSnapshot snapshot, DateTime now)
    {
        if (snapshot.SourceSystem != source) throw new InvalidDataException("Snapshot source does not match.");
        if (!snapshot.IsComplete) throw new InvalidDataException("Snapshot is incomplete; no changes were applied.");
        if (snapshot.CoverageStartUtc.Kind != DateTimeKind.Utc || snapshot.CoverageEndUtc.Kind != DateTimeKind.Utc ||
            snapshot.CoverageStartUtc > snapshot.CoverageEndUtc || snapshot.CoverageEndUtc < now)
            throw new InvalidDataException("Snapshot coverage is invalid or stale.");
        if (snapshot.Transactions.Select(x => x.TransactionId).Distinct().Count() != snapshot.Transactions.Count)
            throw new InvalidDataException("Snapshot contains duplicate transaction identities.");
        foreach (var row in snapshot.Transactions)
        {
            if (row.SourceSystem != source || !ValidText(row.SourceSystem) || !ValidText(row.LocationCode) ||
                !ValidText(row.ProductName) || row.CardNumber is null || row.CardNumber.Length >= 20 ||
                (row.CardNumber.Length != 0 && (row.CardNumber.Length != 4 || !row.CardNumber.All(char.IsAsciiDigit))))
                throw new InvalidDataException("Snapshot contains invalid transaction fields.");
            if (decimal.Round(row.Amount, 2) != row.Amount || Math.Abs(row.Amount) > 9999999999999999.99m)
                throw new InvalidDataException("Transaction amount must fit decimal(18,2).");
            if (row.TransactionTime.Kind != DateTimeKind.Utc || row.TransactionTime > now ||
                row.TransactionTime < snapshot.CoverageStartUtc || row.TransactionTime > snapshot.CoverageEndUtc)
                throw new InvalidDataException("Transaction time is outside the valid UTC coverage.");
        }
    }

    private static bool ValidText(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length < 20;

    private async Task<ImportSourceResult> ReconcileAsync(long runId, DateTime now,
        TransactionSnapshot snapshot, CancellationToken ct)
    {
        // Save this source's records and history together.
        await using var unit = await db.Database.BeginTransactionAsync(ct);
        var existing = await db.Transactions.Where(x => x.SourceSystem == snapshot.SourceSystem)
            .ToDictionaryAsync(x => x.TransactionId, ct);
        var result = new ImportSourceResult { ImportRunId = runId, SourceSystem = snapshot.SourceSystem };
        foreach (var incoming in snapshot.Transactions)
        {
            if (!existing.TryGetValue(incoming.TransactionId, out var record))
            {
                record = new TransactionRecord
                {
                    SourceSystem = snapshot.SourceSystem, TransactionId = incoming.TransactionId,
                    CardNumber = incoming.CardNumber, LocationCode = incoming.LocationCode,
                    ProductName = incoming.ProductName, AmountInCents = (long)(incoming.Amount * 100),
                    TransactionTime = incoming.TransactionTime, CreatedAtUtc = now, UpdatedAtUtc = now
                };
                db.Transactions.Add(record);
                Audit(record, runId, now, "Created", "Status", null, "Active");
                result.Inserted++;
                continue;
            }
            var changed = false;
            void Compare(string field, object oldValue, object newValue)
            {
                if (Equals(oldValue, newValue)) return;
                Audit(record, runId, now, "FieldChanged", field, Format(oldValue), Format(newValue));
                changed = true;
            }
            Compare("CardNumber", record.CardNumber, incoming.CardNumber);
            Compare("LocationCode", record.LocationCode, incoming.LocationCode);
            Compare("ProductName", record.ProductName, incoming.ProductName);
            Compare("Amount", record.AmountInCents / 100m, incoming.Amount);
            Compare("TransactionTime", record.TransactionTime, incoming.TransactionTime);
            if (changed)
            {
                record.CardNumber = incoming.CardNumber;
                record.LocationCode = incoming.LocationCode;
                record.ProductName = incoming.ProductName;
                record.AmountInCents = (long)(incoming.Amount * 100);
                record.TransactionTime = incoming.TransactionTime;
                result.Updated++;
            }
            var restored = record.Status == "Revoked";
            if (restored)
            {
                Audit(record, runId, now, "Restored", "Status", "Revoked", "Active");
                record.Status = "Active";
                result.Restored++;
            }
            if (changed || restored) { record.UpdatedAtUtc = now; record.RevisionNumber++; }
            else result.Unchanged++;
        }
        var incomingIds = snapshot.Transactions.Select(x => x.TransactionId).ToHashSet();
        // Only recent missing records can be revoked.
        var cutoff = now.AddHours(-options.Value.RevocationHours);
        foreach (var record in existing.Values.Where(x => x.Status == "Active" &&
                     x.TransactionTime >= cutoff && x.TransactionTime <= now &&
                     x.TransactionTime >= snapshot.CoverageStartUtc && x.TransactionTime <= snapshot.CoverageEndUtc &&
                     !incomingIds.Contains(x.TransactionId)))
        {
            record.Status = "Revoked";
            record.RevisionNumber++;
            record.UpdatedAtUtc = now;
            Audit(record, runId, now, "Revoked", "Status", "Active", "Revoked");
            result.Revoked++;
        }
        db.ImportSourceResults.Add(result);
        await db.SaveChangesAsync(ct);
        await unit.CommitAsync(ct);
        return result;
    }

    private void Audit(TransactionRecord record, long runId, DateTime now, string type,
        string field, string? before, string? after) => db.AuditChanges.Add(new AuditChange
        {
            Transaction = record, ImportRunId = runId, EventType = type, FieldName = field,
            OldValue = before, NewValue = after, DetectedAtUtc = now
        });

    private static string Format(object value) => value switch
    {
        DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
        decimal money => money.ToString("F2", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
    };
}
