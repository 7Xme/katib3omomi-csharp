using System.Collections.ObjectModel;
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

        FormFields.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasFormFields));
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

    public IEnumerable<TemplateFile> FilteredTemplates =>
        string.IsNullOrWhiteSpace(SearchQuery)
            ? AllTemplates
            : AllTemplates.Where(t => t.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));

    public bool CanShowPreview => SelectedTemplate is not null && LastGenerated is null && !IsParsingTemplate;
    public bool CanShowEmpty => SelectedTemplate is null && !IsParsingTemplate;
    public bool HasFormFields => FormFields.Count > 0;
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

        FormFields.Clear();
        ErrorMessage = null;
        LastGenerated = null;
        DraftPreviewText = string.Empty;
        OriginalTemplateText = string.Empty;
        IsParsingTemplate = true;

        try
        {
            var placeholders = await _docxService.ExtractPlaceholdersAsync(template.FullPath);
            if (template != SelectedTemplate) return;

            foreach (var p in placeholders)
                FormFields.Add(new PlaceholderField { Key = p, Value = string.Empty });

            var plainText = await _docxService.ExtractPlainTextAsync(template.FullPath);
            if (template != SelectedTemplate) return;

            OriginalTemplateText = plainText;
            DraftPreviewText = plainText;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"حدث خطأ أثناء تحليل القالب: {ex.Message}";
        }
        finally
        {
            IsParsingTemplate = false;
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
        if (!string.IsNullOrEmpty(_settingsService.TemplatesFolderPath))
        {
            await LoadTemplatesAsync();
        }
    }

    [RelayCommand]
    private async Task SelectTemplatesFolder()
    {
        var folder = _dialogService.SelectFolder();
        if (folder is null) return;

        _settingsService.TemplatesFolderPath = folder;
        await _settingsService.SaveAsync();
        await LoadTemplatesAsync();
    }

    [RelayCommand]
    private async Task LoadTemplates()
    {
        await LoadTemplatesAsync();
    }

    private async Task LoadTemplatesAsync()
    {
        IsLoadingTemplates = true;
        ErrorMessage = null;

        try
        {
            var path = _settingsService.TemplatesFolderPath;
            if (string.IsNullOrEmpty(path) || !System.IO.Directory.Exists(path))
            {
                AllTemplates.Clear();
                StatusText = "عدد القوالب: 0";
                return;
            }

            var templates = await _templateRepository.LoadTemplatesAsync(path);
            AllTemplates = new ObservableCollection<TemplateFile>(templates);
            StatusText = $"عدد القوالب: {templates.Count}";

            if (templates.Count == 0)
                ErrorMessage = "لا توجد ملفات .docx في المجلد المحدد";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"حدث خطأ أثناء تحميل القوالب: {ex.Message}";
        }
        finally
        {
            IsLoadingTemplates = false;
        }
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

        try
        {
            var values = FormFields.ToDictionary(f => f.Key, f => f.Value);
            var outputDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Katib3omomy", "generated_docs");

            string resultPath;

            if (DraftPreviewText != OriginalTemplateText)
            {
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
        }
        catch (Exception ex)
        {
            ErrorMessage = $"حدث خطأ أثناء إنشاء المستند: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
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
