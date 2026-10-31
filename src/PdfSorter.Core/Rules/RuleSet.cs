namespace PdfSorter.Core.Rules;

/// <summary>Набор правил сортировки — то, что сохраняется в JSON и загружается обратно.</summary>
public sealed class RuleSet
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Общий шаблон имени файла (для правил без собственного шаблона).</summary>
    public string FileNameTemplate { get; set; } = "{name}";

    /// <summary>Папка для файлов, к которым не подошло ни одно правило. Пусто — такие файлы пропускаются.</summary>
    public string FallbackFolder { get; set; } = string.Empty;

    public List<SortRule> Rules { get; set; } = [];

    /// <summary>Набор правил «из коробки»: показывает все типы правил на типичном документообороте.</summary>
    public static RuleSet CreateDefault() => new()
    {
        FileNameTemplate = "{name}",
        FallbackFolder = "Прочее",
        Rules =
        [
            new FileNameRule
            {
                Name = "Счета",
                Pattern = @"^Сч[её]т\D*(\d+)",
                IsRegex = true,
                TargetFolder = "Счета/{yyyy}/{MM}",
                FileNameTemplate = "{date:yyyy-MM-dd}_Счет_{match}",
            },
            new FileNameRule
            {
                Name = "Отчёты",
                Pattern = "Отч?т*",
                TargetFolder = "Отчеты/{yyyy}",
            },
            new PdfTextRule
            {
                Name = "Акты",
                Pattern = "Акт выполненных работ",
                TargetFolder = "Акты/{yyyy}",
                FileNameTemplate = "{date:yyyy-MM-dd}_Акт_{counter:00}",
            },
            new PdfTextRule
            {
                Name = "Договоры",
                Pattern = @"Договор[^№]{0,40}№\s*([\w\-/]+)",
                IsRegex = true,
                TargetFolder = "Договоры/{yyyy}",
                FileNameTemplate = "Договор_{match}",
            },
            new DateRule
            {
                Name = "Архив по дате",
                Source = DateSource.FileName,
                TargetFolder = "Архив/{yyyy}/{MM}",
            },
        ],
    };
}
