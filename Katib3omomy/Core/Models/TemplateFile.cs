using CommunityToolkit.Mvvm.ComponentModel;

namespace Katib3omomy.Core.Models;

public class TemplateFile : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
}
