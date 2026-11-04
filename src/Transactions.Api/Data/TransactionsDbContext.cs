using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Transactions.Api.Models;

namespace Transactions.Api.Data;

public class TransactionsDbContext(DbContextOptions<TransactionsDbContext> options) : DbContext(options)
{
    public DbSet<TransactionRecord> Transactions => Set<TransactionRecord>();
    public DbSet<AuditChange> AuditChanges => Set<AuditChange>();
    public DbSet<ImportRun> ImportRuns => Set<ImportRun>();
    public DbSet<ImportSourceResult> ImportSourceResults => Set<ImportSourceResult>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var transaction = model.Entity<TransactionRecord>();
        transaction.HasIndex(x => new { x.SourceSystem, x.TransactionId }).IsUnique();
        transaction.Property(x => x.SourceSystem).HasMaxLength(19);
        transaction.Property(x => x.CardNumber).HasMaxLength(19);
        transaction.Property(x => x.LocationCode).HasMaxLength(19);
        transaction.Property(x => x.ProductName).HasMaxLength(19);
        transaction.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Transaction_Strings", "length(SourceSystem) BETWEEN 1 AND 19 AND length(CardNumber) < 20 AND length(LocationCode) BETWEEN 1 AND 19 AND length(ProductName) BETWEEN 1 AND 19");
            t.HasCheckConstraint("CK_Transaction_Amount", "AmountInCents BETWEEN -999999999999999999 AND 999999999999999999");
            t.HasCheckConstraint("CK_Transaction_Status", "Status IN ('Active', 'Revoked')");
        });
        model.Entity<AuditChange>().HasOne(x => x.Transaction).WithMany()
            .HasForeignKey(x => x.TransactionRecordId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<AuditChange>().HasOne<ImportRun>().WithMany()
            .HasForeignKey(x => x.ImportRunId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ImportSourceResult>().HasOne<ImportRun>().WithMany()
            .HasForeignKey(x => x.ImportRunId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<AuditChange>().HasIndex(x => new { x.TransactionRecordId, x.Id });
        var utc = new ValueConverter<DateTime, DateTime>(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
        var nullableUtc = new ValueConverter<DateTime?, DateTime?>(x => x,
            x => x.HasValue ? DateTime.SpecifyKind(x.Value, DateTimeKind.Utc) : null);
        foreach (var entity in model.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties())
        {
            if (property.ClrType == typeof(DateTime)) property.SetValueConverter(utc);
            if (property.ClrType == typeof(DateTime?)) property.SetValueConverter(nullableUtc);
        }
    }
}
