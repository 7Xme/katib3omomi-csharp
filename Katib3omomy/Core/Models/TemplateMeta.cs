using System.Text.Json;

namespace Katib3omomy.Core.Models;

public class TemplateMeta
{
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, FieldMeta> Fields { get; set; } = new();

    public static TemplateMeta? Load(string metaPath)
    {
        try
        {
            if (!File.Exists(metaPath)) return null;
            var json = File.ReadAllText(metaPath);
            return JsonSerializer.Deserialize<TemplateMeta>(json);
        }
        catch
        {
            return null;
        }
    }
}

public class FieldMeta
{
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Example { get; set; } = string.Empty;
}
