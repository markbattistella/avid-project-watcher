using AvidProjectWatcher.Core.Models;
using AvidProjectWatcher.Core.Paths;

namespace AvidProjectWatcher.Core.Templates;

public static class FolderTemplateValidator
{
    public static TemplateValidationResult ValidateFlatTemplate(IEnumerable<FolderTemplateEntry> entries)
    {
        var errors = new List<string>();
        var seen = new HashSet<string>(PathUtility.PathComparer);
        var invalidChars = Path.GetInvalidFileNameChars();

        foreach (var entry in entries)
        {
            var value = entry.RelativePath.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add("Folder template entries cannot be empty.");
                continue;
            }

            if (value is "." or "..")
            {
                errors.Add($"Folder template entry '{value}' is not allowed.");
                continue;
            }

            if (value.Contains('/') || value.Contains('\\'))
            {
                errors.Add($"Folder template entry '{value}' must be flat in v1.");
                continue;
            }

            if (value.IndexOfAny(invalidChars) >= 0)
            {
                errors.Add($"Folder template entry '{value}' contains invalid filename characters.");
                continue;
            }

            if (!seen.Add(value))
            {
                errors.Add($"Folder template entry '{value}' is duplicated.");
            }
        }

        return errors.Count == 0 ? TemplateValidationResult.Valid : new TemplateValidationResult(errors);
    }
}
