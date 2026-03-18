using Microsoft.EntityFrameworkCore;
using SharpSentry.Core;

namespace SharpSentry.Infrastructure;

/// <summary>
/// Entity Framework Core <see cref="DbContext"/> for persisting <see cref="MethodMetrics"/>
/// to a SQLite database.
/// </summary>
public sealed class SentryDbContext(DbContextOptions<SentryDbContext> options)
    : DbContext(options)
{
    /// <summary>All persisted method-metrics snapshots.</summary>
    public DbSet<MethodMetricsEntity> MethodMetrics => Set<MethodMetricsEntity>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<MethodMetricsEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MethodName).IsRequired().HasMaxLength(512);
            entity.Property(e => e.FilePath).HasMaxLength(1024);
        });
    }
}
