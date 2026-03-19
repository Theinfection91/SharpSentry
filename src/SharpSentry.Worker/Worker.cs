using SharpSentry.Analysis;
using SharpSentry.Infrastructure;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace SharpSentry.Worker;

/// <summary>
/// Background daemon that watches a directory for C# file changes and triggers analysis.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated by the .NET DI container via AddHostedService.")]
internal sealed class SentryWorker(
    ILogger<SentryWorker> logger,
    CodeAnalyzer analyzer,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration) : BackgroundService
{
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _stoppingCts;
    private readonly ConcurrentDictionary<string, long> _lastEnqueued =
        new(StringComparer.OrdinalIgnoreCase);

    private const long DebounceMs = 500;

    /// <inheritdoc/>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var watchPath = configuration["Sentry:WatchPath"] ?? AppContext.BaseDirectory;

        if (!Directory.Exists(watchPath))
        {
            Log.WatchPathFallback(logger, watchPath);
            watchPath = AppContext.BaseDirectory;
        }

        _watcher = new FileSystemWatcher(watchPath, "*.cs")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;

        Log.WatchingPath(logger, watchPath);

        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Keep the service alive; actual work is driven by FileSystemWatcher events.
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_stoppingCts is not null)
        {
            await _stoppingCts.CancelAsync().ConfigureAwait(false);
        }

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _watcher?.Dispose();
        _stoppingCts?.Dispose();
        base.Dispose();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        var now = Environment.TickCount64;

        // Drop duplicate events fired by FileSystemWatcher for the same save operation.
        if (_lastEnqueued.TryGetValue(e.FullPath, out var last) && now - last < DebounceMs)
            return;

        _lastEnqueued[e.FullPath] = now;

        var token = _stoppingCts?.Token ?? CancellationToken.None;
        _ = Task.Run(() => AnalyseFileAsync(e.FullPath, token), token);
    }

    private async Task AnalyseFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            Log.AnalysingFile(logger, filePath);

            var metrics = await analyzer
                .AnalyseFileAsync(filePath, cancellationToken)
                .ConfigureAwait(false);

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SentryDbContext>();

            foreach (var m in metrics)
            {
                if (m.IsAtRisk)
                {
                    Log.AtRiskMethodDetected(logger, m.ViolationLevel, m.MethodName, m.FilePath, m.LineNumber);
                }
                else
                {
                    Log.MethodOk(logger, m.MethodName, m.CyclomaticComplexity, m.LinesOfCode);
                }

                db.MethodMetrics.Add(MethodMetricsEntity.FromDomain(m));
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.AnalysisFailed(logger, ex, filePath);
        }
    }
}

/// <summary>High-performance logger messages for <see cref="SentryWorker"/>.</summary>
internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Watch path does not exist, falling back to base directory: {Path}")]
    internal static partial void WatchPathFallback(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "SharpSentry is watching {Path} for C# changes.")]
    internal static partial void WatchingPath(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Analysing changed file: {File}")]
    internal static partial void AnalysingFile(ILogger logger, string file);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "At-risk method detected [{Level}]: {Method} in {File}:{Line}")]
    internal static partial void AtRiskMethodDetected(
        ILogger logger, Core.ViolationLevel level, string method, string file, int line);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Method OK: {Method} — CC={Cc}, LOC={Loc}")]
    internal static partial void MethodOk(ILogger logger, string method, int cc, int loc);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to analyse file: {File}")]
    internal static partial void AnalysisFailed(ILogger logger, Exception ex, string file);
}
