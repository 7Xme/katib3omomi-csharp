using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Katib3omomy.ViewModels;
using Microsoft.VisualBasic;

namespace Katib3omomy;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel viewModel)
    {
        _vm = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }

    private void HelpCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/anomalyco/opencode",
            UseShellExecute = true
        });
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_vm.IsDraftModified)
        {
            var result = MessageBox.Show(
                "يوجد نص معدل لم يتم إنشاء مستند منه بعد.\nهل تريد الخروج على أي حال؟",
                "تأكيد الخروج",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                e.Cancel = true;
        }
        base.OnClosing(e);
    }

    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (paths.Length == 0) return;

        var first = paths[0];

        if (Directory.Exists(first))
        {
            await _vm.SetTemplatesFolderAsync(first);
        }
        else if (File.Exists(first) && first.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
        {
            var parent = Path.GetDirectoryName(first);
            if (parent is not null)
            {
                await _vm.SetTemplatesFolderAsync(parent);
                _vm.SelectTemplateByPath(first);
            }
        }
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        if (DraftTextBox.CanUndo)
            DraftTextBox.Undo();
    }

    private void RedoButton_Click(object sender, RoutedEventArgs e)
    {
        if (DraftTextBox.CanRedo)
            DraftTextBox.Redo();
    }

    private async void FileBrowserListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (FileBrowserListBox.SelectedItem is not Core.Models.FileSystemEntry entry) return;

        if (entry.IsFolder)
        {
            await _vm.NavigateToFolderCommand.ExecuteAsync(entry.FullPath);
        }
        else
        {
            _vm.SelectTemplateByPath(entry.FullPath);
        }
    }

    // ---- Editor Toolbar ----

    private void EditorGrid_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            var text = _vm.DraftPreviewText;
            var doc = new FlowDocument();
            var para = new Paragraph();
            para.Inlines.Add(new Run(text));
            doc.Blocks.Add(para);
            doc.FlowDirection = FlowDirection.RightToLeft;
            EditorRichTextBox.Document = doc;
        }
    }

    private void EditorBold_Click(object sender, RoutedEventArgs e)
    {
        EditingCommands.ToggleBold.Execute(null, EditorRichTextBox);
    }

    private void EditorItalic_Click(object sender, RoutedEventArgs e)
    {
        EditingCommands.ToggleItalic.Execute(null, EditorRichTextBox);
    }

    private void EditorUnderline_Click(object sender, RoutedEventArgs e)
    {
        EditingCommands.ToggleUnderline.Execute(null, EditorRichTextBox);
    }

    private void FontSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FontSizeCombo.SelectedItem is ComboBoxItem item &&
            double.TryParse(item.Content.ToString(), out double size))
        {
            EditorRichTextBox.Selection.ApplyPropertyValue(
                System.Windows.Controls.RichTextBox.FontSizeProperty, size);
        }
    }

    private void InsertPlaceholder_Click(object sender, RoutedEventArgs e)
    {
        var name = Interaction.InputBox(
            "أدخل اسم الحقل الجديد (بدون *):",
            "إدراج حقل جديد",
            "field_name");
        if (string.IsNullOrWhiteSpace(name)) return;

        name = name.Trim();
        var placeholder = $"*{name}*";
        EditorRichTextBox.CaretPosition.InsertTextInRun(placeholder);

        if (!_vm.FormFields.Any(f => f.Key == name))
        {
            _vm.FormFields.Add(new Core.Models.PlaceholderField { Key = name, Value = string.Empty });
        }
    }

    private void PrintPreviewViewer_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            var doc = new FlowDocument();
            var para = new Paragraph();
            para.Inlines.Add(new Run(_vm.DraftPreviewText));
            para.TextAlignment = TextAlignment.Right;
            doc.FlowDirection = FlowDirection.RightToLeft;
            doc.FontFamily = new System.Windows.Media.FontFamily("Arial");
            doc.FontSize = 14;
            doc.PageWidth = 793.7;
            doc.PageHeight = 1122.5;
            doc.PagePadding = new Thickness(96);
            doc.Blocks.Add(para);
            PrintPreviewViewer.Document = doc;
        }
    }

    private void EditorSave_Click(object sender, RoutedEventArgs e)
    {
        var textRange = new TextRange(
            EditorRichTextBox.Document.ContentStart,
            EditorRichTextBox.Document.ContentEnd);
        var plainText = textRange.Text.Trim();

        if (!string.IsNullOrEmpty(plainText))
        {
            _vm.DraftPreviewText = plainText;
        }

        _vm.ToggleEditorCommand.Execute(null);
    }
}
