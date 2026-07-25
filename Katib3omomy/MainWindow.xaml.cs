using System.IO;
using System.Windows;
using Katib3omomy.ViewModels;

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
}
