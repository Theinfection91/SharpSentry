using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharpSentry.Core;

namespace SharpSentry.Infrastructure;

/// <summary>
/// Entity Framework Core <see cref="DbContext"/> for persisting <see cref="MethodMetrics"/>
/// to a SQLite database.
/// </summary>
public sealed class SentryDbContext : DbContext
{
    public SentryDbContext(DbContextOptions<SentryDbContext> options)
        : base(options)
    {
        Database.EnsureCreated();
    }

    /// <summary>All persisted method-metrics snapshots.</summary>
    public DbSet<MethodMetricsEntity> MethodMetrics { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<MethodMetricsEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MethodName).IsRequired().HasMaxLength(512);
            entity.Property(e => e.FilePath).HasMaxLength(1024);

            // Optional: index common queries
            entity.HasIndex(e => new { e.FilePath, e.MethodName });
            entity.HasIndex(e => e.AnalysedAt);
        });
    }
}

/// <summary>
/// Design-time factory used by EF tools (dotnet ef) to create a <see cref="SentryDbContext"/>.
/// Reads an optional environment variable SENTRY_DB_PATH or falls back to 'sharpSentry.db' in the app base directory.
/// </summary>
#pragma warning disable CA1812 // Instantiated by EF tools via reflection; not directly referenced in code.
internal sealed class SentryDbContextFactory : IDesignTimeDbContextFactory<SentryDbContext>
{
    public SentryDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<SentryDbContext>();

        var dbPath = Environment.GetEnvironmentVariable("SENTRY_DB_PATH")
                     ?? Path.Combine(AppContext.BaseDirectory, "sharpSentry.db");

        var connectionString = $"Data Source={dbPath}";

        builder.UseSqlite(connectionString);

        return new SentryDbContext(builder.Options);
    }
}
#pragma warning restore CA1812

/// <summary>
/// Small convenience extension to register the DB context in DI with sensible defaults.
/// Looks for configuration keys:
/// - Sentry:ConnectionString (full connection string)
/// - Sentry:DatabasePath (path to SQLite file)
/// If neither is provided, defaults to SQLite file 'sharpSentry.db' in the app base directory.
/// </summary>
public static class SentryDbContextExtensions
{
    public static IServiceCollection AddSentryPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetSection("Sentry:ConnectionString").Value;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var dbPath = configuration.GetSection("Sentry:DatabasePath").Value
                         ?? Path.Combine(AppContext.BaseDirectory, "sharpSentry.db");

            connectionString = $"Data Source={dbPath}";
        }

        services.AddDbContext<SentryDbContext>(opts => opts.UseSqlite(connectionString));
        return services;
    }
}
