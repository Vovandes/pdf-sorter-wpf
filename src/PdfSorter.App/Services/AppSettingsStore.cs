using System.IO;
using System.IO.Abstractions;
using System.Text.Json;
using PdfSorter.Core.Journal;
using PdfSorter.Core.Rules;
using PdfSorter.Core.Serialization;

namespace PdfSorter.App.Services;

/// <summary>Состояние окна между запусками: папки, режим и текущий набор правил.</summary>
public sealed class AppSettings
{
    public string SourceFolder { get; set; } = string.Empty;

    public bool IncludeSubfolders { get; set; } = true;

    public string TargetFolder { get; set; } = string.Empty;

    public FileOperationMode Mode { get; set; } = FileOperationMode.Move;

    public RuleSet RuleSet { get; set; } = RuleSet.CreateDefault();
}

public sealed class AppSettingsStore
{
    private readonly IFileSystem _fileSystem;
    private readonly string _path;

    public AppSettingsStore(IFileSystem fileSystem, string path)
    {
        _fileSystem = fileSystem;
        _path = path;
    }

    /// <summary>Загружает настройки; при отсутствии или повреждении файла возвращает значения по умолчанию.</summary>
    public AppSettings Load()
    {
        try
        {
            if (_fileSystem.File.Exists(_path)
                && JsonSerializer.Deserialize<AppSettings>(_fileSystem.File.ReadAllText(_path), JsonDefaults.Options) is { } settings)
            {
                return settings;
            }
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            // Повреждённые настройки не должны мешать запуску — начинаем с чистого листа.
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(_path)!);
            _fileSystem.File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonDefaults.Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Не удалось сохранить настройки — не критично, при следующем запуске будут прежние.
        }
    }
}
