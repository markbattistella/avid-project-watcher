namespace AvidProjectWatcher.Core.Templates;

public sealed record TemplateValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static TemplateValidationResult Valid { get; } = new([]);
}
