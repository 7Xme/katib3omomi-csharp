namespace Katib3omomy.Core.Models;

public class FileSystemEntry
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public bool IsDrive { get; set; }
    public string Icon => IsFolder ? "\U0001F4C1" : "\U0001F4C4";
}
