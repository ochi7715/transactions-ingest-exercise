namespace Transactions.Api.Sources;

public sealed class DemoData(DateTime anchorUtc)
{
    public DateTime AnchorUtc { get; } = anchorUtc;
}

public class MockTransactionSource(string sourceSystem, string locationCode, DemoData data,
    TimeProvider clock) : ITransactionSource
{
    public string SourceSystem => sourceSystem;

    public Task<TransactionSnapshot> GetSnapshotAsync(string scenario, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (scenario == "failure" && sourceSystem == "Online")
            throw new HttpRequestException("The Online demo source is unavailable.");
        var corrected = scenario is "corrected" or "missing" or "restored";
        var rows = new List<SourceTransaction>
        {
            new(sourceSystem, 100001, "1111", locationCode, "Wireless Mouse",
                corrected ? 29.99m : 24.99m, data.AnchorUtc),
            new(sourceSystem, 100002, "", locationCode, "Keyboard", 89.99m, data.AnchorUtc.AddMinutes(-15)),
            new(sourceSystem, 900001, "4444", locationCode, "USB-C Dock", 129.95m, data.AnchorUtc.AddHours(-26))
        };
        if (corrected)
            rows.Add(new(sourceSystem, 100003, "0002", locationCode, "Monitor Stand", 45m, data.AnchorUtc.AddMinutes(10)));
        if (scenario == "missing" && sourceSystem == "Store Uptown")
            rows.RemoveAll(x => x.TransactionId is 100002 or 900001);
        if (scenario == "incomplete") rows.Clear();
        return Task.FromResult(new TransactionSnapshot(sourceSystem, scenario != "incomplete",
            DateTime.UnixEpoch, clock.GetUtcNow().UtcDateTime, rows));
    }
}
