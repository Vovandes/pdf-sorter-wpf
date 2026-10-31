using System.Text.Json.Serialization;
using PdfSorter.Core.Naming;

namespace PdfSorter.Core.Rules;

/// <summary>
/// Правило сортировки. Правила проверяются по порядку, срабатывает первое подошедшее:
/// файл попадает в <see cref="TargetFolder"/> и получает имя по <see cref="FileNameTemplate"/>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(FileNameRule), "fileName")]
[JsonDerivedType(typeof(DateRule), "date")]
[JsonDerivedType(typeof(PdfTextRule), "pdfText")]
public abstract class SortRule
{
    protected SortRule(string kindTitle)
    {
        KindTitle = kindTitle;
    }

    [JsonPropertyOrder(-2)]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyOrder(-1)]
    public bool IsEnabled { get; set; } = true;

    /// <summary>Шаблон папки относительно целевой, например <c>Счета/{yyyy}/{MM}</c>. Пусто — корень целевой папки.</summary>
    [JsonPropertyOrder(10)]
    public string TargetFolder { get; set; } = string.Empty;

    /// <summary>Шаблон имени файла для этого правила. Пусто — используется общий шаблон набора правил.</summary>
    [JsonPropertyOrder(11)]
    public string? FileNameTemplate { get; set; }

    /// <summary>Тип правила для отображения в интерфейсе: «Имя файла», «Дата», «Текст PDF».</summary>
    [JsonIgnore]
    public string KindTitle { get; }

    /// <summary>Название правила, а если оно не задано — его краткое описание.</summary>
    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Describe() : Name;

    /// <summary>Проверяет файл. Возвращает <c>null</c>, если правило не подошло.</summary>
    public abstract RuleMatch? Match(FileCandidate file);

    /// <summary>Условие правила коротко, например <c>Счет_*</c> или <c>«Акт выполненных работ»</c>.</summary>
    public abstract string DescribeCondition();

    /// <summary>Тип и условие правила, например «Имя файла: Счет_*».</summary>
    public string Describe() => $"{KindTitle}: {DescribeCondition()}";

    /// <summary>Ошибки настройки правила (пустой список — правило корректно).</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        ValidateCondition(errors);
        errors.AddRange(PathTemplate.Validate(TargetFolder).Select(e => $"папка: {e}"));
        errors.AddRange(PathTemplate.Validate(FileNameTemplate).Select(e => $"имя файла: {e}"));
        return errors;
    }

    protected abstract void ValidateCondition(ICollection<string> errors);
}
