using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.ML;
using SharpSentry.Core;

namespace SharpSentry.Analysis;

/// <summary>
/// Analyses C# source files using Roslyn and ML.NET to produce <see cref="MethodMetrics"/>
/// and predict <see cref="ViolationLevel"/> for each method.
/// </summary>
public sealed class CodeAnalyzer
{
    private readonly MLContext _mlContext;

    /// <summary>Initialises a new <see cref="CodeAnalyzer"/> with an optional ML.NET random seed.</summary>
    /// <param name="mlSeed">Random seed passed to <see cref="MLContext"/> for reproducibility.</param>
    public CodeAnalyzer(int? mlSeed = null)
    {
        _mlContext = new MLContext(seed: mlSeed);
    }

    /// <summary>
    /// Parses the given C# <paramref name="sourceText"/> and returns one <see cref="MethodMetrics"/>
    /// record per method declaration found.
    /// </summary>
    /// <param name="sourceText">Raw C# source code to analyse.</param>
    /// <param name="filePath">
    /// Optional file path that will be stored in each returned <see cref="MethodMetrics.FilePath"/>.
    /// </param>
    /// <returns>A read-only list of metrics, one per method declaration.</returns>
    public IReadOnlyList<MethodMetrics> Analyse(string sourceText, string filePath = "")
    {
        ArgumentNullException.ThrowIfNull(sourceText);

        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetCompilationUnitRoot();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        var results = new List<MethodMetrics>();
        var now = DateTimeOffset.UtcNow;

        foreach (var method in methods)
        {
            var metrics = BuildMetrics(method, filePath, now, _mlContext);
            results.Add(metrics);
        }

        return results.AsReadOnly();
    }

    /// <summary>
    /// Asynchronously analyses a file on disk.
    /// </summary>
    /// <param name="filePath">Absolute path to the <c>.cs</c> file.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>A read-only list of <see cref="MethodMetrics"/> for each method in the file.</returns>
    public async Task<IReadOnlyList<MethodMetrics>> AnalyseFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var source = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        return Analyse(source, filePath);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static MethodMetrics BuildMetrics(
        MethodDeclarationSyntax method,
        string filePath,
        DateTimeOffset analysedAt,
        MLContext mlContext)
    {
        var methodName = ResolveFullName(method);
        var lineSpan = method.GetLocation().GetLineSpan();
        var lineNumber = lineSpan.StartLinePosition.Line + 1;

        var linesOfCode = CountLogicalLines(method);
        var paramCount = method.ParameterList.Parameters.Count;
        var cyclomatic = ComputeCyclomaticComplexity(method);
        var nesting = ComputeMaxNesting(method);
        var cognitive = ComputeCognitiveComplexity(method);
        var violation = ClassifyViolation(mlContext, cyclomatic, linesOfCode, paramCount, nesting, cognitive);

        return new MethodMetrics(
            MethodName: methodName,
            FilePath: filePath,
            LineNumber: lineNumber,
            CyclomaticComplexity: cyclomatic,
            LinesOfCode: linesOfCode,
            ParameterCount: paramCount,
            NestingDepth: nesting,
            CognitiveComplexity: cognitive,
            ViolationLevel: violation,
            AnalysedAt: analysedAt);
    }

    private static string ResolveFullName(MethodDeclarationSyntax method)
    {
        var typeName = method.Ancestors()
                             .OfType<BaseTypeDeclarationSyntax>()
                             .FirstOrDefault()
                             ?.Identifier.Text ?? "<unknown>";

        var ns = method.Ancestors()
                       .OfType<BaseNamespaceDeclarationSyntax>()
                       .FirstOrDefault()
                       ?.Name.ToString() ?? string.Empty;

        var prefix = ns.Length > 0 ? $"{ns}.{typeName}" : typeName;
        return $"{prefix}.{method.Identifier.Text}";
    }

    private static int CountLogicalLines(MethodDeclarationSyntax method)
    {
        if (method.Body is null)
        {
            return 0;
        }

        return method.Body.Statements.Count;
    }

    private static int ComputeCyclomaticComplexity(MethodDeclarationSyntax method)
    {
        // Start at 1 and add 1 for each branching node.
        int complexity = 1;
        foreach (var node in method.DescendantNodes())
        {
            complexity += node switch
            {
                IfStatementSyntax => 1,
                WhileStatementSyntax => 1,
                ForStatementSyntax => 1,
                ForEachStatementSyntax => 1,
                DoStatementSyntax => 1,
                CaseSwitchLabelSyntax => 1,
                CasePatternSwitchLabelSyntax => 1,
                WhenClauseSyntax => 1,
                ConditionalExpressionSyntax => 1,
                BinaryExpressionSyntax bin
                    when bin.IsKind(SyntaxKind.LogicalAndExpression)
                      || bin.IsKind(SyntaxKind.LogicalOrExpression) => 1,
                CatchClauseSyntax => 1,
                _ => 0,
            };
        }

        return complexity;
    }

    private static int ComputeMaxNesting(SyntaxNode node, int currentDepth = 0)
    {
        int max = currentDepth;
        foreach (var child in node.ChildNodes())
        {
            bool increments = child is IfStatementSyntax
                                    or WhileStatementSyntax
                                    or ForStatementSyntax
                                    or ForEachStatementSyntax
                                    or DoStatementSyntax
                                    or SwitchStatementSyntax
                                    or TryStatementSyntax;

            int childDepth = increments ? currentDepth + 1 : currentDepth;
            int childMax = ComputeMaxNesting(child, childDepth);
            if (childMax > max)
            {
                max = childMax;
            }
        }

        return max;
    }

    private static int ComputeCognitiveComplexity(MethodDeclarationSyntax method)
    {
        // Simplified Sonar-style cognitive complexity:
        // +1 per structural element, +nesting for nested elements.
        int score = 0;
        ComputeCognitiveRecursive(method, 0, ref score);
        return score;
    }

    private static void ComputeCognitiveRecursive(SyntaxNode node, int nestingLevel, ref int score)
    {
        foreach (var child in node.ChildNodes())
        {
            bool isStructural = child is IfStatementSyntax
                                       or WhileStatementSyntax
                                       or ForStatementSyntax
                                       or ForEachStatementSyntax
                                       or DoStatementSyntax
                                       or SwitchStatementSyntax
                                       or CatchClauseSyntax;

            if (isStructural)
            {
                score += 1 + nestingLevel;
                ComputeCognitiveRecursive(child, nestingLevel + 1, ref score);
            }
            else
            {
                ComputeCognitiveRecursive(child, nestingLevel, ref score);
            }
        }
    }

    private static ViolationLevel ClassifyViolation(
        MLContext mlContext,
        int cyclomatic,
        int linesOfCode,
        int paramCount,
        int nesting,
        int cognitive)
    {
        // mlContext is reserved for future ML-powered classification.
        // The current implementation uses heuristic thresholds.
        _ = mlContext;

        if (cyclomatic > 20 || linesOfCode > 100 || nesting > 6 || cognitive > 30)
        {
            return ViolationLevel.Critical;
        }

        if (cyclomatic > 15 || linesOfCode > 75 || paramCount > 7 || nesting > 5 || cognitive > 20)
        {
            return ViolationLevel.Error;
        }

        if (cyclomatic > 10 || linesOfCode > 50 || paramCount > 5 || nesting > 4 || cognitive > 15)
        {
            return ViolationLevel.Warning;
        }

        return ViolationLevel.Info;
    }
}
