using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Katib3omomy.Core.Services;

public class DialogService : IDialogService
{
    public string? SelectFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "اختر مجلد النماذج"
        };

        if (dialog.ShowDialog() == true)
            return dialog.FolderName;

        return null;
    }

    public void ShowError(string message)
    {
        MessageBox.Show(message, "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public bool ShowConfirm(string title, string message)
    {
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public void ShowSuccess(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void OpenFile(string path)
    {
        if (!File.Exists(path)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] DialogService.OpenFile: {ex.Message}");
        }
    }

    public void PrintFile(string path)
    {
        OpenFile(path);
    }

    public void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] DialogService.CopyToClipboard: {ex.Message}");
        }
    }
}
