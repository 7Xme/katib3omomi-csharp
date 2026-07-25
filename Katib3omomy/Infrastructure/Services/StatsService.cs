using Katib3omomy.Core.Models;
using Katib3omomy.Core.Services;

namespace Katib3omomy.Infrastructure.Services;

public class StatsService : IStatsService
{
    private readonly StatsData _stats;
    private readonly System.Timers.Timer _timer;

    public StatsData Stats => _stats;

    public StatsService()
    {
        _stats = StatsData.Load();
        _stats.SessionStart = DateTime.Now;

        _timer = new System.Timers.Timer(60000);
        _timer.Elapsed += (_, _) =>
        {
            _stats.TotalSessionSeconds += 60;
            _stats.Save();
        };
        _timer.Start();
    }

    public void RecordGeneration()
    {
        _stats.TotalDocumentsGenerated++;
        _stats.Save();
    }

    public string FormatUsageTime()
    {
        var totalMinutes = (int)(_stats.TotalSessionSeconds / 60);
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        return hours > 0
            ? $"{hours} ساعة و {minutes} دقيقة"
            : $"{minutes} دقيقة";
    }

    public int GetTemplateCount()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = System.IO.Path.Combine(appData, "Katib3omomy");
            var settingsPath = System.IO.Path.Combine(folder, "settings.json");
            if (System.IO.File.Exists(settingsPath))
            {
                var json = System.IO.File.ReadAllText(settingsPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("TemplatesFolderPath", out var pathProp))
                {
                    var templatesDir = pathProp.GetString();
                    if (!string.IsNullOrEmpty(templatesDir) && System.IO.Directory.Exists(templatesDir))
                    {
                        return System.IO.Directory.GetFiles(templatesDir, "*.docx").Length;
                    }
                }
            }
        }
        catch { }
        return 0;
    }
}
