using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Transactions.Api.Data;
using Transactions.Api.Services;
using Transactions.Api.Sources;

namespace Transactions.Api.Controllers;

public record ImportRequest(string Scenario = "baseline");

[ApiController]
[Route("api/import")]
public class ImportController(ImportService service, TransactionsDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Import([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ImportRequest? request, CancellationToken ct)
    {
        var scenario = request?.Scenario ?? "baseline";
        if (!DemoScenarios.All.Contains(scenario))
            return Problem(statusCode: 400, title: "Unknown scenario", detail: string.Join(", ", DemoScenarios.All));
        try { return Ok(await service.ImportAsync(scenario, ct)); }
        catch (ImportAlreadyRunningException)
        { return Problem(statusCode: 409, title: "An import is already running. Try again shortly."); }
    }

    [HttpGet("scenarios")]
    public IActionResult Scenarios() => Ok(DemoScenarios.All);

    [HttpGet("runs")]
    public async Task<IActionResult> Runs(CancellationToken ct)
    {
        var runs = await db.ImportRuns.AsNoTracking().OrderByDescending(x => x.Id).Take(20).ToListAsync(ct);
        var ids = runs.Select(x => x.Id).ToArray();
        var results = await db.ImportSourceResults.AsNoTracking().Where(x => ids.Contains(x.ImportRunId)).ToListAsync(ct);
        return Ok(runs.Select(x => new ImportSummary(x.Id, x.StartedAtUtc, x.CompletedAtUtc,
            x.Scenario, x.Status, results.Where(y => y.ImportRunId == x.Id).ToList())));
    }
}
