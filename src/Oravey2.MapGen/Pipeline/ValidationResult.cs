namespace Oravey2.MapGen.Pipeline;

public sealed class ValidationResult
{
    public List<ValidationItem> Items { get; } = [];

    public bool Passed => Items.All(i => i.Severity != ValidationSeverity.Error);
    public int ErrorCount => Items.Count(i => i.Severity == ValidationSeverity.Error);
    public int WarningCount => Items.Count(i => i.Severity == ValidationSeverity.Warning);
    public int PassCount => Items.Count(i => i.Severity == ValidationSeverity.Pass);

    public void AddPass(string check, string detail) =>
        Items.Add(new ValidationItem(ValidationSeverity.Pass, check, detail));

    public void AddWarning(string check, string detail) =>
        Items.Add(new ValidationItem(ValidationSeverity.Warning, check, detail));

    public void AddError(string check, string detail) =>
        Items.Add(new ValidationItem(ValidationSeverity.Error, check, detail));
}

public sealed record ValidationItem(
    ValidationSeverity Severity,
    string Check,
    string Detail);

public enum ValidationSeverity
{
    Pass,
    Warning,
    Error,
}
