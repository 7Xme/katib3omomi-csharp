using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Katib3omomy.Core.Models;
using Katib3omomy.Core.Services;
using Katib3omomy.Infrastructure.Data;

namespace Katib3omomy.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ITemplateRepository _templateRepository;
    private readonly ISettingsService _settingsService;
    private readonly IDocxPlaceholderService _docxService;
    private readonly IDialogService _dialogService;
    private readonly string _backupFilePath;
    private readonly DispatcherTimer _autoSaveTimer;

    public MainViewModel(
        ISettingsService settingsService,
        ITemplateRepository templateRepository,
        IDocxPlaceholderService docxService,
        IDialogService dialogService)
    {
        _settingsService = settingsService;
        _templateRepository = templateRepository;
        _docxService = docxService;
        _dialogService = dialogService;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "Katib3omomy");
        Directory.CreateDirectory(folder);
        _backupFilePath = Path.Combine(folder, "draft_backup.json");

        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _autoSaveTimer.Tick += async (_, _) => await SaveBackupAsync();

        FormFields.CollectionChanged += (_, e) =>
        {
            OnPropertyChanged(nameof(HasFormFields));
            OnPropertyChanged(nameof(CanGenerate));
            if (e.NewItems is not null)
                foreach (PlaceholderField field in e.NewItems)
                    field.PropertyChanged += OnFieldPropertyChanged;
            if (e.OldItems is not null)
                foreach (PlaceholderField field in e.OldItems)
                    field.PropertyChanged -= OnFieldPropertyChanged;
        };
    }

    private async Task SaveBackupAsync()
    {
        if (SelectedTemplate is null || !IsDraftModified) return;
        try
        {
            var data = new BackupData
            {
                TemplatePath = SelectedTemplate.FullPath,
                OriginalText = OriginalTemplateText,
                DraftText = DraftPreviewText
            };
            var json = JsonSerializer.Serialize(data);
            await File.WriteAllTextAsync(_backupFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] SaveBackup: {ex.Message}");
        }
    }

    private async Task<BackupData?> LoadBackupAsync()
    {
        try
        {
            if (!File.Exists(_backupFilePath)) return null;
            var json = await File.ReadAllTextAsync(_backupFilePath);
            return JsonSerializer.Deserialize<BackupData>(json);
        }
        catch
        {
            return null;
        }
    }

    private void ClearBackup()
    {
        try
        {
            if (File.Exists(_backupFilePath))
                File.Delete(_backupFilePath);
        }
        catch { }
    }

    private class BackupData
    {
        public string? TemplatePath { get; set; }
        public string? OriginalText { get; set; }
        public string? DraftText { get; set; }
    }

    public ObservableCollection<string> RecentFolders { get; } = new();

    public void RefreshRecentFolders()
    {
        RecentFolders.Clear();
        foreach (var f in _settingsService.RecentFolders)
            RecentFolders.Add(f);
        OnPropertyChanged(nameof(HasRecentFolders));
    }

    private void OnFieldPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaceholderField.HasError))
            OnPropertyChanged(nameof(CanGenerate));
    }

    [ObservableProperty]
    private ObservableCollection<TemplateFile> _allTemplates = new();

    [ObservableProperty]
    private ObservableCollection<PlaceholderField> _formFields = new();

    [ObservableProperty]
    private TemplateFile? _selectedTemplate;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _draftPreviewText = string.Empty;

    [ObservableProperty]
    private string _originalTemplateText = string.Empty;

    [ObservableProperty]
    private GeneratedDocument? _lastGenerated;

    [ObservableProperty]
    private bool _isLoadingTemplates;

    [ObservableProperty]
    private bool _isParsingTemplate;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _progressDescription = string.Empty;

    [ObservableProperty]
    private bool _isEditingTemplate;

    partial void OnIsEditingTemplateChanged(bool value)
    {
        OnPropertyChanged(nameof(CanShowPreview));
    }

    [ObservableProperty]
    private string _currentFolderPath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FileSystemEntry> _fileBrowserItems = new();

    public bool HasRecentFolders => RecentFolders.Count > 0;
    public bool CanGoUp => !string.IsNullOrEmpty(CurrentFolderPath) && Directory.GetParent(CurrentFolderPath) is not null;

    public IEnumerable<TemplateFile> FilteredTemplates =>
        string.IsNullOrWhiteSpace(SearchQuery)
            ? AllTemplates
            : AllTemplates.Where(t =>
                System.Globalization.CultureInfo.CurrentCulture.CompareInfo.IndexOf(
                    t.Name, SearchQuery, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0);

    public bool CanShowPreview => SelectedTemplate is not null && LastGenerated is null && !IsParsingTemplate && !IsEditingTemplate;
    public bool CanShowEmpty => SelectedTemplate is null && !IsParsingTemplate;
    public bool HasFormFields => FormFields.Count > 0;
    public bool CanGenerate => HasFormFields && FormFields.All(f => !f.HasError);
    public bool IsDraftModified => DraftPreviewText != OriginalTemplateText;
    public bool HasSelectedTemplate => SelectedTemplate is not null;
    public bool HasLastGenerated => LastGenerated is not null;

    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredTemplates));
    }

    partial void OnSelectedTemplateChanged(TemplateFile? value)
    {
        OnPropertyChanged(nameof(FilteredTemplates));
        OnPropertyChanged(nameof(CanShowPreview));
        OnPropertyChanged(nameof(CanShowEmpty));
        OnPropertyChanged(nameof(HasSelectedTemplate));

        if (value is not null)
            _ = LoadTemplateDataAsync(value);
    }

    private async Task LoadTemplateDataAsync(TemplateFile template)
    {
        if (template != SelectedTemplate) return;

        ClearBackup();
        _autoSaveTimer.Stop();
        FormFields.Clear();
        ErrorMessage = null;
        LastGenerated = null;
        DraftPreviewText = string.Empty;
        OriginalTemplateText = string.Empty;
        IsParsingTemplate = true;
        ProgressDescription = "جاري تحليل القالب...";

        try
        {
            var placeholders = await _docxService.ExtractPlaceholdersAsync(template.FullPath);
            if (template != SelectedTemplate) return;

            foreach (var p in placeholders)
                FormFields.Add(new PlaceholderField { Key = p, Value = string.Empty });

            ProgressDescription = "جاري استخراج النص...";

            var plainText = await _docxService.ExtractPlainTextAsync(template.FullPath);
            if (template != SelectedTemplate) return;

            OriginalTemplateText = plainText;
            DraftPreviewText = plainText;
            ProgressDescription = string.Empty;
            _autoSaveTimer.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] LoadTemplateDataAsync: {ex}");
            ErrorMessage = "حدث خطأ أثناء تحليل القالب. تأكد من أن الملف بصيغة .docx صالحة.";
        }
        finally
        {
            IsParsingTemplate = false;
            OnPropertyChanged(nameof(CanGenerate));
        }
    }

    partial void OnLastGeneratedChanged(GeneratedDocument? value)
    {
        OnPropertyChanged(nameof(CanShowPreview));
        OnPropertyChanged(nameof(CanShowEmpty));
        OnPropertyChanged(nameof(HasLastGenerated));
    }

    partial void OnIsParsingTemplateChanged(bool value)
    {
        OnPropertyChanged(nameof(CanShowPreview));
        OnPropertyChanged(nameof(CanShowEmpty));
    }

    partial void OnDraftPreviewTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsDraftModified));
    }

    partial void OnOriginalTemplateTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsDraftModified));
    }

    public async Task InitializeAsync()
    {
        await _settingsService.LoadAsync();
        RefreshRecentFolders();
        if (!string.IsNullOrEmpty(_settingsService.TemplatesFolderPath))
        {
            var path = _settingsService.TemplatesFolderPath;
            if (Directory.Exists(path))
                await NavigateToFolderAsync(path);
            else
                await ShowDriveListAsync();
            await TryRestoreBackupAsync();
        }
        else
        {
            await ShowDriveListAsync();
        }
    }

    private async Task ShowDriveListAsync()
    {
        CurrentFolderPath = string.Empty;
        OnPropertyChanged(nameof(CanGoUp));
        FileBrowserItems = new ObservableCollection<FileSystemEntry>(
            DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => new FileSystemEntry
                {
                    Name = d.Name.TrimEnd('\\'),
                    FullPath = d.RootDirectory.FullName,
                    IsFolder = true,
                    IsDrive = true
                }));
        AllTemplates.Clear();
        StatusText = string.Empty;
    }

    private async Task TryRestoreBackupAsync()
    {
        var backup = await LoadBackupAsync();
        if (backup is null || string.IsNullOrEmpty(backup.TemplatePath)) return;

        var template = AllTemplates.FirstOrDefault(t =>
            string.Equals(t.FullPath, backup.TemplatePath, StringComparison.OrdinalIgnoreCase));
        if (template is null) return;

        if (string.IsNullOrEmpty(backup.DraftText) || backup.DraftText == backup.OriginalText)
        {
            ClearBackup();
            return;
        }

        var restore = _dialogService.ShowConfirm(
            "استعادة النص المحفوظ",
            $"يوجد نص معدل غير محفوظ من الجلسة السابقة للقالب:\n{template.Name}\n\nهل تريد استعادته؟");
        if (restore)
        {
            SelectedTemplate = template;
            OriginalTemplateText = backup.OriginalText ?? string.Empty;
            DraftPreviewText = backup.DraftText ?? string.Empty;
        }
        else
        {
            ClearBackup();
        }
    }

    [RelayCommand]
    private async Task SelectRecentFolder(string folderPath)
    {
        await NavigateToFolderAsync(folderPath);
    }

    [RelayCommand]
    private async Task NavigateToFolder(string path)
    {
        await NavigateToFolderAsync(path);
    }

    [RelayCommand]
    private async Task GoUp()
    {
        if (string.IsNullOrEmpty(CurrentFolderPath)) return;
        var parent = Directory.GetParent(CurrentFolderPath);
        if (parent is not null)
            await NavigateToFolderAsync(parent.FullName);
    }

    private async Task NavigateToFolderAsync(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;

        CurrentFolderPath = path;
        OnPropertyChanged(nameof(CanGoUp));
        _settingsService.AddRecentFolder(path);
        await _settingsService.SaveAsync();
        RefreshRecentFolders();
        await LoadTemplatesFromFolderAsync(path);
    }

    private async Task LoadTemplatesFromFolderAsync(string path)
    {
        IsLoadingTemplates = true;
        ErrorMessage = null;

        try
        {
            var items = new ObservableCollection<FileSystemEntry>();

            foreach (var dir in Directory.GetDirectories(path))
            {
                var info = new DirectoryInfo(dir);
                items.Add(new FileSystemEntry
                {
                    Name = info.Name,
                    FullPath = dir,
                    IsFolder = true
                });
            }

            foreach (var file in Directory.GetFiles(path, "*.docx"))
            {
                var info = new FileInfo(file);
                items.Add(new FileSystemEntry
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    FullPath = file,
                    IsFolder = false
                });
            }

            FileBrowserItems = items;

            var templates = items
                .Where(i => !i.IsFolder)
                .Select(i => new TemplateFile
                {
                    Name = i.Name,
                    FileName = Path.GetFileName(i.FullPath),
                    FullPath = i.FullPath
                }).ToList();

            AllTemplates = new ObservableCollection<TemplateFile>(templates);
            StatusText = $"عدد القوالب: {templates.Count}";

            if (templates.Count == 0)
                ErrorMessage = "لا توجد ملفات .docx في هذا المجلد";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] LoadTemplatesFromFolder: {ex}");
            ErrorMessage = "حدث خطأ أثناء تصفح المجلد.";
        }
        finally
        {
            IsLoadingTemplates = false;
        }
    }

    public async Task SetTemplatesFolderAsync(string path)
    {
        await NavigateToFolderAsync(path);
    }

    [RelayCommand]
    private async Task SelectTemplatesFolder()
    {
        var folder = _dialogService.SelectFolder();
        if (folder is null) return;
        await SetTemplatesFolderAsync(folder);
    }

    [RelayCommand]
    private async Task LoadTemplates()
    {
        if (!string.IsNullOrEmpty(CurrentFolderPath) && Directory.Exists(CurrentFolderPath))
            await NavigateToFolderAsync(CurrentFolderPath);
        else
            await ShowDriveListAsync();
    }

    public void SelectTemplateByPath(string filePath)
    {
        SelectedTemplate = AllTemplates.FirstOrDefault(t =>
            string.Equals(t.FullPath, filePath, StringComparison.OrdinalIgnoreCase));
    }

    [RelayCommand]
    private async Task GenerateDocument()
    {
        var emptyFields = FormFields.Where(f => string.IsNullOrWhiteSpace(f.Value)).Select(f => f.Key).ToList();
        if (emptyFields.Count > 0)
        {
            ErrorMessage = $"يرجى ملء جميع الحقول: {string.Join("، ", emptyFields)}";
            return;
        }

        if (SelectedTemplate is null) return;

        IsGenerating = true;
        ErrorMessage = null;
        ProgressDescription = "جاري إنشاء المستند...";

        try
        {
            var values = FormFields.ToDictionary(f => f.Key, f => f.Value);
            var outputDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Katib3omomy", "generated_docs");

            string resultPath;

            if (DraftPreviewText != OriginalTemplateText)
            {
                if (_docxService.TemplateHasTables(SelectedTemplate.FullPath))
                {
                    var proceed = _dialogService.ShowConfirm(
                        "تعديل النص سيُفقد الجداول",
                        "يحتوي هذا القالب على جداول. التوليد من النص المعدل سينشئ مستنداً نصياً فقط بدون جداول.\n\nهل تريد المتابعة؟");
                    if (!proceed)
                    {
                        IsGenerating = false;
                        return;
                    }
                }

                resultPath = await _docxService.GenerateDocumentFromPlainTextAsync(
                    DraftPreviewText, values, outputDir, SelectedTemplate.Name);
            }
            else
            {
                resultPath = await _docxService.GenerateDocumentAsync(
                    SelectedTemplate.FullPath, values, outputDir, SelectedTemplate.Name);
            }

            LastGenerated = new GeneratedDocument
            {
                FilePath = resultPath,
                FileName = System.IO.Path.GetFileName(resultPath),
                GeneratedAt = DateTime.Now,
                TemplateName = SelectedTemplate.Name
            };

            _dialogService.ShowSuccess("تم بنجاح", $"تم إنشاء المستند بنجاح:\n{LastGenerated.FileName}");
            ClearBackup();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] GenerateDocument: {ex}");
            ErrorMessage = "حدث خطأ أثناء إنشاء المستند. حاول مرة أخرى أو تأكد من القالب.";
        }
        finally
        {
            IsGenerating = false;
            ProgressDescription = string.Empty;
        }
    }

    [RelayCommand]
    private void ClearForm()
    {
        foreach (var f in FormFields)
            f.Value = string.Empty;
        LastGenerated = null;
    }

    [RelayCommand]
    private void ResetSelection()
    {
        _autoSaveTimer.Stop();
        ClearBackup();
        SelectedTemplate = null;
        FormFields.Clear();
        LastGenerated = null;
        DraftPreviewText = string.Empty;
        OriginalTemplateText = string.Empty;
        SearchQuery = string.Empty;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void ResetDraft()
    {
        DraftPreviewText = OriginalTemplateText;
    }

    [RelayCommand]
    private void ToggleEditor()
    {
        IsEditingTemplate = !IsEditingTemplate;
    }

    [RelayCommand]
    private void CopyPreview()
    {
        _dialogService.CopyToClipboard(DraftPreviewText);
    }

    [RelayCommand]
    private void OpenDocument()
    {
        if (LastGenerated is not null)
            _dialogService.OpenFile(LastGenerated.FilePath);
    }

    [RelayCommand]
    private void PrintDocument()
    {
        if (LastGenerated is not null)
            _dialogService.PrintFile(LastGenerated.FilePath);
    }
}
