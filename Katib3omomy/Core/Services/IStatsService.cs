using Katib3omomy.Core.Models;

namespace Katib3omomy.Core.Services;

public interface IStatsService
{
    StatsData Stats { get; }
    void RecordGeneration();
    string FormatUsageTime();
    int GetTemplateCount();
}
