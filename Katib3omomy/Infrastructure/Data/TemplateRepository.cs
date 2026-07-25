using System.IO;
using Katib3omomy.Core.Models;

namespace Katib3omomy.Infrastructure.Data;

public class TemplateRepository : ITemplateRepository
{
    public Task<List<TemplateFile>> LoadTemplatesAsync(string folderPath)
    {
        var templates = new List<TemplateFile>();

        if (!Directory.Exists(folderPath))
            return Task.FromResult(templates);

        var files = Directory.GetFiles(folderPath, "*.docx", SearchOption.TopDirectoryOnly);

        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var fileName = Path.GetFileName(file);

            templates.Add(new TemplateFile
            {
                Name = name,
                FileName = fileName,
                FullPath = file
            });
        }

        templates.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(templates);
    }
}
