using System.Diagnostics;

namespace PdfSorter.App.Services;

/// <summary>
/// Диалоги для неинтерактивного запуска (режим скриншотов): подтверждения принимаются автоматически,
/// сообщения уходят в Trace, а выбор файлов не поддерживается.
/// </summary>
public sealed class AutomationDialogService : IDialogService
{
    public List<string> Messages { get; } = [];

    public string? PickFolder(string title, string? initialFolder) => null;

    public string? PickOpenFile(string title, string filter) => null;

    public string? PickSaveFile(string title, string filter, string defaultFileName) => null;

    public bool Confirm(string title, string message)
    {
        Record("confirm", title, message);
        return true;
    }

    public void ShowInfo(string title, string message) => Record("info", title, message);

    public void ShowWarning(string title, string message) => Record("warning", title, message);

    public void ShowError(string title, string message) => Record("error", title, message);

    public void OpenFolder(string path) => Record("open", path, string.Empty);

    private void Record(string kind, string title, string message)
    {
        var line = $"[{kind}] {title}: {message}";
        Messages.Add(line);
        Trace.WriteLine(line);
    }
}
