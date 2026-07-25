namespace Katib3omomy.Core.Services;

public interface IDialogService
{
    string? SelectFolder();
    void ShowError(string message);
    bool ShowConfirm(string title, string message);
    void ShowSuccess(string title, string message);
    void OpenFile(string path);
    void PrintFile(string path);
    void CopyToClipboard(string text);
}
