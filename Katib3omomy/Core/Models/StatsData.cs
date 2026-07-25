using System.Text.Json;

namespace Katib3omomy.Core.Models;

public class StatsData
{
    public int TotalDocumentsGenerated { get; set; }
    public DateTime FirstUsedDate { get; set; } = DateTime.Now;
    public DateTime SessionStart { get; set; } = DateTime.Now;
    public double TotalSessionSeconds { get; set; }

    private static readonly string FilePath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Katib3omomy", "stats.json");

    public static StatsData Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<StatsData>(json) ?? new StatsData();
            }
        }
        catch { }
        return new StatsData();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
