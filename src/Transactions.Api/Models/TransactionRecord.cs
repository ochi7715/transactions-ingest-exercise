namespace Transactions.Api.Models;

public class TransactionRecord
{
    public long Id { get; set; }
    public string SourceSystem { get; set; } = "";
    public int TransactionId { get; set; }
    public string CardNumber { get; set; } = "";
    public string LocationCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public long AmountInCents { get; set; }
    public DateTime TransactionTime { get; set; }
    public string Status { get; set; } = "Active";
    public int RevisionNumber { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class AuditChange
{
    public long Id { get; set; }
    public long TransactionRecordId { get; set; }
    public TransactionRecord Transaction { get; set; } = null!;
    public long ImportRunId { get; set; }
    public string EventType { get; set; } = "";
    public string FieldName { get; set; } = "";
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime DetectedAtUtc { get; set; }
}

public class ImportRun
{
    public long Id { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string Scenario { get; set; } = "";
    public string Status { get; set; } = "Running";
}

public class ImportSourceResult
{
    public long Id { get; set; }
    public long ImportRunId { get; set; }
    public string SourceSystem { get; set; } = "";
    public string Status { get; set; } = "Succeeded";
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Revoked { get; set; }
    public int Restored { get; set; }
    public int Unchanged { get; set; }
    public string? Error { get; set; }
}
