using System.Globalization;
using System.Text.RegularExpressions;

namespace PdfSorter.Core.Naming;

/// <summary>
/// Шаблоны папок и имён файлов с подстановками:
/// <c>{name}</c>, <c>{ext}</c>, <c>{date:yyyy-MM-dd}</c>, <c>{counter:000}</c>, <c>{match}</c>, <c>{rule}</c>,
/// а также сокращения даты <c>{yyyy}</c>, <c>{yy}</c>, <c>{MM}</c>, <c>{dd}</c>.
/// </summary>
public static partial class PathTemplate
{
    /// <summary>Культура форматирования дат (названия месяцев в <c>{date:MMMM}</c> — по-русски).</summary>
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("ru-RU");

    public const string DefaultDateFormat = "yyyy-MM-dd";

    /// <summary>Подсказка для интерфейса.</summary>
    public const string PlaceholdersHelp =
        "{name} {ext} {date:yyyy-MM-dd} {counter:000} {match} {rule} {yyyy} {MM} {dd}";

    private static readonly string[] CaseInsensitiveKeys = ["name", "ext", "date", "counter", "match", "rule"];
    private static readonly string[] DatePartKeys = ["yyyy", "yy", "MM", "dd"];

    /// <summary>
    /// Строит имя файла. Если в шаблоне нет <c>{ext}</c> и результат не оканчивается исходным расширением,
    /// расширение добавляется автоматически. Пустой результат заменяется исходным именем.
    /// </summary>
    public static string RenderFileName(string template, TemplateContext context)
    {
        var effectiveTemplate = string.IsNullOrWhiteSpace(template) ? "{name}" : template;
        var rendered = Render(effectiveTemplate, context, EscapeSeparators).Trim();

        var extensionSuffix = context.Extension.Length > 0 ? "." + context.Extension : string.Empty;
        if (extensionSuffix.Length > 0 && rendered.EndsWith(extensionSuffix, StringComparison.OrdinalIgnoreCase))
        {
            extensionSuffix = rendered[^extensionSuffix.Length..]; // сохраняем регистр, указанный в шаблоне
            rendered = rendered[..^extensionSuffix.Length];
        }

        var stem = string.IsNullOrWhiteSpace(rendered.Trim('.', '_', '-')) ? context.Name : rendered;
        return FileNameSanitizer.SanitizeFileName(stem + extensionSuffix);
    }

    /// <summary>
    /// Строит относительный путь папки: <c>/</c> и <c>\</c> в шаблоне — разделители уровней,
    /// каждый уровень очищается отдельно, поэтому выйти за пределы целевой папки нельзя.
    /// Разделители внутри подставленных значений заменяются на <c>_</c>.
    /// </summary>
    public static string RenderFolder(string template, TemplateContext context)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        var rendered = Render(template, context, EscapeSeparators);
        var segments = rendered
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(FileNameSanitizer.SanitizeSegment);
        return string.Join(Path.DirectorySeparatorChar, segments);
    }

    /// <summary>Проверяет шаблон: неизвестные подстановки и некорректные форматы.</summary>
    public static IReadOnlyList<string> Validate(string? template)
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(template))
        {
            return errors;
        }

        foreach (Match token in TokenPattern().Matches(template))
        {
            var key = token.Groups["key"].Value;
            var format = token.Groups["format"].Success ? token.Groups["format"].Value : null;
            var normalized = NormalizeKey(key);
            if (normalized is null)
            {
                errors.Add($"неизвестная подстановка {token.Value}");
                continue;
            }

            if (format is not null && !IsValidFormat(normalized, format))
            {
                errors.Add($"некорректный формат в {token.Value}");
            }
        }

        if (template.Count(c => c == '{') != template.Count(c => c == '}'))
        {
            errors.Add("непарные фигурные скобки");
        }

        return errors;
    }

    private static string Render(string template, TemplateContext context, Func<string, string> encodeValue) =>
        TokenPattern().Replace(template, token =>
        {
            var key = NormalizeKey(token.Groups["key"].Value);
            var format = token.Groups["format"].Success ? token.Groups["format"].Value : null;
            var value = key is null ? null : Resolve(key, format, context);
            return value is null ? token.Value : encodeValue(value);
        });

    private static string? Resolve(string key, string? format, TemplateContext context)
    {
        try
        {
            return key switch
            {
                "name" => context.Name,
                "ext" => context.Extension,
                "date" => context.Date.ToString(string.IsNullOrEmpty(format) ? DefaultDateFormat : format, Culture),
                "counter" => context.Counter.ToString(string.IsNullOrEmpty(format) ? "0" : format, Culture),
                "match" => context.Match ?? string.Empty,
                "rule" => context.RuleName ?? string.Empty,
                _ => context.Date.ToString(key, Culture), // yyyy, yy, MM, dd
            };
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? NormalizeKey(string key)
    {
        var caseInsensitive = CaseInsensitiveKeys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
        return caseInsensitive ?? DatePartKeys.FirstOrDefault(k => k.Equals(key, StringComparison.Ordinal));
    }

    private static bool IsValidFormat(string key, string format)
    {
        try
        {
            switch (key)
            {
                case "date":
                    _ = new DateTime(2025, 3, 14).ToString(format, Culture);
                    return true;
                case "counter":
                    _ = 7.ToString(format, Culture);
                    return true;
                default:
                    return false; // у остальных подстановок формата нет
            }
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string EscapeSeparators(string value) =>
        value.Replace('/', FileNameSanitizer.Replacement).Replace('\\', FileNameSanitizer.Replacement);

    [GeneratedRegex(@"\{(?<key>[A-Za-z]+)(?::(?<format>[^{}]*))?\}", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
