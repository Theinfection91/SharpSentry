using SharpSentry.Core;

namespace SharpSentry.Infrastructure;

/// <summary>
/// Persistable entity that mirrors <see cref="MethodMetrics"/> for EF Core storage.
/// </summary>
public sealed class MethodMetricsEntity
{
    /// <summary>Primary key (auto-increment).</summary>
    public int Id { get; set; }

    /// <summary><inheritdoc cref="MethodMetrics.MethodName"/></summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary><inheritdoc cref="MethodMetrics.FilePath"/></summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary><inheritdoc cref="MethodMetrics.LineNumber"/></summary>
    public int LineNumber { get; set; }

    /// <summary><inheritdoc cref="MethodMetrics.CyclomaticComplexity"/></summary>
    public int CyclomaticComplexity { get; set; }

    /// <summary><inheritdoc cref="MethodMetrics.LinesOfCode"/></summary>
    public int LinesOfCode { get; set; }

    /// <summary><inheritdoc cref="MethodMetrics.ParameterCount"/></summary>
    public int ParameterCount { get; set; }

    /// <summary><inheritdoc cref="MethodMetrics.NestingDepth"/></summary>
    public int NestingDepth { get; set; }

    /// <summary><inheritdoc cref="MethodMetrics.CognitiveComplexity"/></summary>
    public int CognitiveComplexity { get; set; }

    /// <summary><inheritdoc cref="MethodMetrics.ViolationLevel"/></summary>
    public ViolationLevel ViolationLevel { get; set; }

    /// <summary><inheritdoc cref="MethodMetrics.AnalysedAt"/></summary>
    public DateTimeOffset AnalysedAt { get; set; }

    /// <summary>
    /// Creates a <see cref="MethodMetricsEntity"/> from a domain <see cref="MethodMetrics"/> record.
    /// </summary>
    public static MethodMetricsEntity FromDomain(MethodMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        return new MethodMetricsEntity
        {
            MethodName = metrics.MethodName,
            FilePath = metrics.FilePath,
            LineNumber = metrics.LineNumber,
            CyclomaticComplexity = metrics.CyclomaticComplexity,
            LinesOfCode = metrics.LinesOfCode,
            ParameterCount = metrics.ParameterCount,
            NestingDepth = metrics.NestingDepth,
            CognitiveComplexity = metrics.CognitiveComplexity,
            ViolationLevel = metrics.ViolationLevel,
            AnalysedAt = metrics.AnalysedAt,
        };
    }

    /// <summary>Projects this entity back to a domain <see cref="MethodMetrics"/> record.</summary>
    public MethodMetrics ToDomain() =>
        new(
            MethodName: MethodName,
            FilePath: FilePath,
            LineNumber: LineNumber,
            CyclomaticComplexity: CyclomaticComplexity,
            LinesOfCode: LinesOfCode,
            ParameterCount: ParameterCount,
            NestingDepth: NestingDepth,
            CognitiveComplexity: CognitiveComplexity,
            ViolationLevel: ViolationLevel,
            AnalysedAt: AnalysedAt);
}
