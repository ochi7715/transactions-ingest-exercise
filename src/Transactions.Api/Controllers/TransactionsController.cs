using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Transactions.Api.Data;

namespace Transactions.Api.Controllers;

[ApiController]
[Route("api/transactions")]
public class TransactionsController(TransactionsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var records = await db.Transactions.AsNoTracking().OrderByDescending(x => x.TransactionTime)
            .ThenBy(x => x.SourceSystem).ThenBy(x => x.TransactionId).ToListAsync(ct);
        return Ok(records.Select(x => new
        {
            x.Id, x.SourceSystem, x.TransactionId, x.CardNumber, x.LocationCode, x.ProductName,
            Amount = x.AmountInCents / 100m, x.TransactionTime, x.Status, x.RevisionNumber,
            x.CreatedAtUtc, x.UpdatedAtUtc
        }));
    }

    [HttpGet("{id:long}/audit")]
    public async Task<IActionResult> Audit(long id, CancellationToken ct)
    {
        if (!await db.Transactions.AnyAsync(x => x.Id == id, ct)) return NotFound();
        return Ok(await db.AuditChanges.AsNoTracking().Where(x => x.TransactionRecordId == id)
            .OrderByDescending(x => x.Id).Select(x => new
            {
                x.Id, x.ImportRunId, x.EventType, x.FieldName, x.OldValue, x.NewValue, x.DetectedAtUtc
            }).ToListAsync(ct));
    }
}
