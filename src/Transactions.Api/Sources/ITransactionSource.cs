namespace Transactions.Api.Sources;

public record SourceTransaction(string SourceSystem, int TransactionId, string CardNumber,
    string LocationCode, string ProductName, decimal Amount, DateTime TransactionTime);

public record TransactionSnapshot(string SourceSystem, bool IsComplete, DateTime CoverageStartUtc,
    DateTime CoverageEndUtc, IReadOnlyList<SourceTransaction> Transactions);

public interface ITransactionSource
{
    string SourceSystem { get; }
    Task<TransactionSnapshot> GetSnapshotAsync(string scenario, CancellationToken cancellationToken);
}

public static class DemoScenarios
{
    public static readonly string[] All = ["baseline", "corrected", "missing", "restored", "failure", "incomplete"];
}
