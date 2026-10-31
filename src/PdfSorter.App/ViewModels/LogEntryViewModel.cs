namespace PdfSorter.App.ViewModels;

public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>Строка журнала операций в окне.</summary>
public sealed record LogEntryViewModel(DateTime Time, LogLevel Level, string Text)
{
    public string TimeText => Time.ToString("HH:mm:ss");

    public string Glyph => Level switch
    {
        LogLevel.Success => "✔",
        LogLevel.Warning => "!",
        LogLevel.Error => "✖",
        _ => "•",
    };
}
