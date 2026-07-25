namespace Katib3omomy.Core.Services;

public interface ISettingsService
{
    string? TemplatesFolderPath { get; set; }
    List<string> RecentFolders { get; }
    void AddRecentFolder(string folderPath);
    Task LoadAsync();
    Task SaveAsync();
}
