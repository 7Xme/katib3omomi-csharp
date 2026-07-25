using Katib3omomy.Core.Models;
using Katib3omomy.Core.Services;
using Katib3omomy.Infrastructure.Data;

namespace Katib3omomy.Infrastructure.Services;

public class StatsService : IStatsService
{
    private readonly StatsData _stats;
    private readonly ITemplateRepository _templateRepo;
    private readonly System.Timers.Timer _timer;

    public StatsData Stats => _stats;

    public StatsService(ITemplateRepository templateRepo)
    {
        _templateRepo = templateRepo;
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
            return _templateRepository.GetAll().Count();
        }
        catch
        {
            return 0;
        }
    }
}
