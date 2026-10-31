namespace PdfSorter.App.Services;

/// <summary>Диалоги и взаимодействие с оболочкой — ViewModel не зависит от окон напрямую.</summary>
public interface IDialogService
{
    string? PickFolder(string title, string? initialFolder);

    string? PickOpenFile(string title, string filter);

    string? PickSaveFile(string title, string filter, string defaultFileName);

    bool Confirm(string title, string message);

    void ShowInfo(string title, string message);

    void ShowWarning(string title, string message);

    void ShowError(string title, string message);

    /// <summary>Открывает папку в Проводнике.</summary>
    void OpenFolder(string path);
}
