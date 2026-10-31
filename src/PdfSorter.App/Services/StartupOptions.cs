using System.Globalization;
using PdfSorter.Core.Journal;

namespace PdfSorter.App.Services;

/// <summary>Что сделать сразу после запуска (для демонстрации и автоматических скриншотов).</summary>
public enum AutomationStage
{
    None,
    Main,
    Preview,
    DryRun,
    Run,
    Undo,
}

/// <summary>
/// Аргументы командной строки. Пример:
/// <c>PdfSorter.exe --data-dir .local --source samples --target sorted --rules samples\rules.json --stage preview --screenshot docs\screenshot_preview.png</c>
/// </summary>
public sealed class StartupOptions
{
    public const string Usage = """
        Аргументы (все необязательные):
          --source <папка>        исходная папка
          --target <папка>        целевая папка
          --rules <файл.json>     загрузить набор правил
          --subfolders | --no-subfolders
          --mode move|copy        перемещать или копировать
          --data-dir <папка>      где хранить журналы и настройки (по умолчанию %LOCALAPPDATA%\PdfSorter)
          --stage main|preview|dryrun|run|undo   выполнить действие сразу после запуска
          --select-rule <номер>   выделить правило в редакторе
          --screenshot <файл.png> сохранить снимок окна и закрыться
          --scale <число>         масштаб снимка (по умолчанию 1)
          --width <px> --height <px>  размер окна
        """;

    public string? SourceFolder { get; private set; }

    public string? TargetFolder { get; private set; }

    public string? RulesFile { get; private set; }

    public bool? IncludeSubfolders { get; private set; }

    public FileOperationMode? Mode { get; private set; }

    public string? DataDirectory { get; private set; }

    public AutomationStage Stage { get; private set; }

    public int? SelectedRule { get; private set; }

    public string? ScreenshotPath { get; private set; }

    public double ScreenshotScale { get; private set; } = 1;

    public double? WindowWidth { get; private set; }

    public double? WindowHeight { get; private set; }

    /// <summary>Неинтерактивный запуск: диалоги подтверждаются автоматически, настройки не сохраняются.</summary>
    public bool IsAutomation => ScreenshotPath is not null || Stage != AutomationStage.None;

    /// <exception cref="ArgumentException">Неизвестный аргумент или некорректное значение.</exception>
    public static StartupOptions Parse(IReadOnlyList<string> args)
    {
        var options = new StartupOptions();
        for (var i = 0; i < args.Count; i++)
        {
            var name = args[i];
            string Value() => i + 1 < args.Count ? args[++i] : throw new ArgumentException($"Для {name} не указано значение.");

            switch (name.ToLowerInvariant())
            {
                case "--source": options.SourceFolder = Value(); break;
                case "--target": options.TargetFolder = Value(); break;
                case "--rules": options.RulesFile = Value(); break;
                case "--subfolders": options.IncludeSubfolders = true; break;
                case "--no-subfolders": options.IncludeSubfolders = false; break;
                case "--mode": options.Mode = ParseEnum<FileOperationMode>(name, Value()); break;
                case "--data-dir": options.DataDirectory = Value(); break;
                case "--stage": options.Stage = ParseEnum<AutomationStage>(name, Value()); break;
                case "--select-rule": options.SelectedRule = (int)ParseNumber(name, Value()); break;
                case "--screenshot": options.ScreenshotPath = Value(); break;
                case "--scale": options.ScreenshotScale = ParseNumber(name, Value()); break;
                case "--width": options.WindowWidth = ParseNumber(name, Value()); break;
                case "--height": options.WindowHeight = ParseNumber(name, Value()); break;
                default: throw new ArgumentException($"Неизвестный аргумент: {name}");
            }
        }

        return options;
    }

    private static T ParseEnum<T>(string name, string value)
        where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var result) && Enum.IsDefined(result)
            ? result
            : throw new ArgumentException($"Некорректное значение {name}: {value}");

    private static double ParseNumber(string name, string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number > 0
            ? number
            : throw new ArgumentException($"Некорректное число в {name}: {value}");
}
