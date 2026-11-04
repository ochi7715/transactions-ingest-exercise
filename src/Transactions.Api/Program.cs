using Microsoft.EntityFrameworkCore;
using Transactions.Api.Data;
using Transactions.Api.Services;
using Transactions.Api.Sources;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<TransactionsDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Transactions") ?? "Data Source=transactions.db"));
builder.Services.AddOptions<ImportOptions>().BindConfiguration("Import")
    .Validate(x => x.RevocationHours > 0 && x.RevocationHours <= 168, "RevocationHours must be between 1 and 168.")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp =>
{
    using var scope = sp.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
    var anchor = db.Transactions.Where(x => x.TransactionId == 100001)
        .Select(x => (DateTime?)x.TransactionTime).FirstOrDefault();
    return new DemoData(anchor ?? sp.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddHours(-1));
});
builder.Services.AddSingleton<ImportGate>();
builder.Services.AddScoped<ImportService>();
foreach (var (source, location) in new[] { ("Store Uptown", "UPTOWN"), ("Store Downtown", "DOWNTOWN"), ("Online", "ONLINE") })
{
    builder.Services.AddSingleton<ITransactionSource>(sp => new MockTransactionSource(source, location,
        sp.GetRequiredService<DemoData>(), sp.GetRequiredService<TimeProvider>()));
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
    db.Database.Migrate();
}

app.MapControllers();

app.Run();

public partial class Program;
