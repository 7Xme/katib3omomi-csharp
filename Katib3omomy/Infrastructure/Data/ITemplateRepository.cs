using Katib3omomy.Core.Models;

namespace Katib3omomy.Infrastructure.Data;

public interface ITemplateRepository
{
    Task<List<TemplateFile>> LoadTemplatesAsync(string folderPath);
}
