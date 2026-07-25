namespace Katib3omomy.Core.Services;

public interface IDocxPlaceholderService
{
    Task<List<string>> ExtractPlaceholdersAsync(string filePath);
    Task<string> ExtractPlainTextAsync(string filePath);
    Task<string> GenerateDocumentAsync(string templatePath, Dictionary<string, string> values, string outputDir, string baseFileName);
    Task<string> GenerateDocumentFromPlainTextAsync(string content, Dictionary<string, string> values, string outputDir, string baseFileName);
    bool IsValidDocx(string filePath);
    bool TemplateHasTables(string filePath);
}
