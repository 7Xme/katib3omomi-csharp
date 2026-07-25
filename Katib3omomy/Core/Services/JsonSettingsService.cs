using System.IO;
using System.Text.Json;

namespace Katib3omomy.Core.Services;

public class JsonSettingsService : ISettingsService
{
    private readonly string _settingsFilePath;

    private JsonSettingsData _data = new();

    public string? TemplatesFolderPath
    {
        get => _data.TemplatesFolderPath;
        set => _data.TemplatesFolderPath = value;
    }

    public List<string> RecentFolders => _data.RecentFolders;

    public void AddRecentFolder(string folderPath)
    {
        _data.RecentFolders.RemoveAll(f => string.Equals(f, folderPath, StringComparison.OrdinalIgnoreCase));
        _data.RecentFolders.Insert(0, folderPath);
        if (_data.RecentFolders.Count > 5)
            _data.RecentFolders.RemoveRange(5, _data.RecentFolders.Count - 5);
    }

    public JsonSettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "Katib3omomy");
        Directory.CreateDirectory(folder);
        _settingsFilePath = Path.Combine(folder, "settings.json");
    }

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                _data = JsonSerializer.Deserialize<JsonSettingsData>(json) ?? new JsonSettingsData();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] JsonSettingsService.LoadAsync: {ex.Message}");
            _data = new JsonSettingsData();
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] JsonSettingsService.SaveAsync: {ex.Message}");
        }
    }

    private class JsonSettingsData
    {
        public string? TemplatesFolderPath { get; set; }
        public List<string> RecentFolders { get; set; } = new();
    }
}
