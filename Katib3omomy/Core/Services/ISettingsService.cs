namespace Katib3omomy.Core.Services;

public interface ISettingsService
{
    string? TemplatesFolderPath { get; set; }
    Task LoadAsync();
    Task SaveAsync();
}
