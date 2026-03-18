namespace SharpSentry.Core;

/// <summary>
/// Immutable snapshot of static-analysis metrics for a single C# method.
/// </summary>
/// <param name="MethodName">Fully-qualified name of the method (e.g. <c>MyApp.Services.OrderService.PlaceOrder</c>).</param>
/// <param name="FilePath">Absolute or solution-relative path to the source file.</param>
/// <param name="LineNumber">1-based line number where the method declaration starts.</param>
/// <param name="CyclomaticComplexity">McCabe cyclomatic complexity of the method body.</param>
/// <param name="LinesOfCode">Logical (non-blank, non-comment) lines of code in the method.</param>
/// <param name="ParameterCount">Number of declared parameters.</param>
/// <param name="NestingDepth">Maximum nesting depth of control-flow blocks inside the method.</param>
/// <param name="CognitiveComplexity">Cognitive complexity score (Sonar metric) for the method.</param>
/// <param name="ViolationLevel">Highest <see cref="SharpSentry.Core.ViolationLevel"/> raised for this method.</param>
/// <param name="AnalysedAt">UTC timestamp when metrics were captured.</param>
public sealed record MethodMetrics(
    string MethodName,
    string FilePath,
    int LineNumber,
    int CyclomaticComplexity,
    int LinesOfCode,
    int ParameterCount,
    int NestingDepth,
    int CognitiveComplexity,
    ViolationLevel ViolationLevel,
    DateTimeOffset AnalysedAt)
{
    /// <summary>
    /// Returns <see langword="true"/> when any metric exceeds a safe threshold,
    /// indicating the method warrants investigation.
    /// </summary>
    public bool IsAtRisk =>
        CyclomaticComplexity > 10 ||
        LinesOfCode > 50 ||
        ParameterCount > 5 ||
        NestingDepth > 4 ||
        CognitiveComplexity > 15 ||
        ViolationLevel >= ViolationLevel.Error;
}
