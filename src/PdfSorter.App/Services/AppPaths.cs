using System.IO;

namespace PdfSorter.App.Services;

/// <summary>
/// Где приложение хранит данные. По умолчанию <c>%LOCALAPPDATA%\PdfSorter</c>; можно переопределить
/// аргументом <c>--data-dir</c> или переменной окружения <c>PDFSORTER_DATA_DIR</c>.
/// </summary>
public sealed record AppPaths(string DataDirectory)
{
    public const string DataDirectoryVariable = "PDFSORTER_DATA_DIR";

    public string LogsDirectory => Path.Combine(DataDirectory, "logs");

    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public static AppPaths Resolve(string? overrideDirectory)
    {
        var directory = overrideDirectory
            ?? Environment.GetEnvironmentVariable(DataDirectoryVariable)
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PdfSorter");
        return new AppPaths(Path.GetFullPath(directory));
    }
}
