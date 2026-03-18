namespace SharpSentry.Core;

/// <summary>Severity level of a code-analysis violation.</summary>
public enum ViolationLevel
{
    /// <summary>Informational finding; no action required.</summary>
    Info = 0,

    /// <summary>Minor issue that should be reviewed.</summary>
    Warning = 1,

    /// <summary>Serious issue that should be addressed promptly.</summary>
    Error = 2,

    /// <summary>Critical security or stability risk requiring immediate action.</summary>
    Critical = 3,
}
