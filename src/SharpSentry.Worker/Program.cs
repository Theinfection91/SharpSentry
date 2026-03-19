using System;
using System.IO;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SharpSentry.Analysis;
using SharpSentry.Infrastructure;
using SharpSentry.Worker;

internal sealed class Program
{
    private static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton<CodeAnalyzer>();
        builder.Services.AddSentryPersistence(builder.Configuration);
        builder.Services.AddHostedService<SentryWorker>();

        var host = builder.Build();

        var dbPath = ResolveDbPath(builder.Configuration);

        using (var scope = host.Services.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            ProgramLog.SentryDbPath(logger, dbPath);

            // Ensure DB created (SentryDbContext constructor calls EnsureCreated).
            scope.ServiceProvider.GetRequiredService<SentryDbContext>();
        }

        host.Run();
    }

    private static string ResolveDbPath(ConfigurationManager configuration)
    {
        var connectionString = configuration.GetSection("Sentry:ConnectionString").Value;
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            try
            {
                var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

                if (builder.TryGetValue("Data Source", out var ds) ||
                    builder.TryGetValue("DataSource", out ds) ||
                    builder.TryGetValue("Filename", out ds) ||
                    builder.TryGetValue("FileName", out ds))
                {
                    return ds?.ToString() ?? connectionString;
                }

                return connectionString;
            }
            catch (ArgumentException)
            {
                // Malformed connection string — fall back to the raw value.
                return connectionString;
            }
            catch (InvalidOperationException)
            {
                // Unexpected state from the provider — fall back to the raw value.
                return connectionString;
            }
        }

        return configuration.GetSection("Sentry:DatabasePath").Value
               ?? Path.Combine(AppContext.BaseDirectory, "sharpSentry.db");
    }
}

internal static partial class ProgramLog
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Sentry DB path: {Path}")]
    internal static partial void SentryDbPath(ILogger logger, string path);
}