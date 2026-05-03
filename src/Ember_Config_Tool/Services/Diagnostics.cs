namespace Ember_Config_Tool.Services;

public enum DiagnosticSeverity
{
    Warning,
    Blocking
}

public sealed record ToolDiagnostic
{
    public ToolDiagnostic(DiagnosticSeverity severity, string path, string message)
        : this(severity, path, path, message, message)
    {
    }

    public ToolDiagnostic(
        DiagnosticSeverity severity,
        string displayPath,
        string technicalPath,
        string displayMessage,
        string technicalMessage)
    {
        Severity = severity;
        DisplayPath = displayPath;
        TechnicalPath = technicalPath;
        DisplayMessage = displayMessage;
        TechnicalMessage = technicalMessage;
    }

    public DiagnosticSeverity Severity { get; }
    public string DisplayPath { get; }
    public string TechnicalPath { get; }
    public string DisplayMessage { get; }
    public string TechnicalMessage { get; }
    public bool IsBlocking => Severity == DiagnosticSeverity.Blocking;
    public string DisplayText => ToString();
    public string TechnicalText => string.IsNullOrWhiteSpace(TechnicalPath)
        ? TechnicalMessage
        : $"{TechnicalPath}: {TechnicalMessage}";

    public override string ToString()
    {
        var prefix = IsBlocking ? "BLOCKING" : "WARNING";
        return string.IsNullOrWhiteSpace(DisplayPath)
            ? $"{prefix}: {DisplayMessage}"
            : $"{prefix}: {DisplayPath}: {DisplayMessage}";
    }
}
