namespace Katib3omomy.Core.Models;

public class HistoryEntry
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}
