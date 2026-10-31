using PdfSorter.Core.Matching;
using PdfSorter.Core.Text;

namespace PdfSorter.Core.Rules;

/// <summary>
/// Правило по тексту первой страницы PDF: ключевое слово или регулярное выражение.
/// Первая группа выражения (например, номер договора) доступна в шаблонах как <c>{match}</c>.
/// </summary>
public sealed class PdfTextRule : SortRule
{
    public PdfTextRule()
        : base("Текст PDF")
    {
    }

    /// <summary>Ключевое слово (<c>Акт выполненных работ</c>) или выражение (<c>Договор\s*№\s*(\S+)</c>).</summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary><c>true</c> — <see cref="Pattern"/> является регулярным выражением, иначе ключевым словом.</summary>
    public bool IsRegex { get; set; }

    public bool IgnoreCase { get; set; } = true;

    public override RuleMatch? Match(FileCandidate file)
    {
        if (string.IsNullOrWhiteSpace(Pattern))
        {
            return null;
        }

        var text = file.GetFirstPageText();
        if (IsRegex)
        {
            var match = RegexMatcher.Match(text, Pattern, IgnoreCase);
            return match.Success ? new RuleMatch(RegexMatcher.ExtractValue(match)) : null;
        }

        var keyword = TextNormalizer.CollapseWhitespace(Pattern);
        var comparison = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var index = TextNormalizer.CollapseWhitespace(text).IndexOf(keyword, comparison);
        return index >= 0 ? new RuleMatch(keyword) : null;
    }

    public override string DescribeCondition() => IsRegex ? $"/{Pattern}/" : $"«{Pattern}»";

    protected override void ValidateCondition(ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(Pattern))
        {
            errors.Add("не задан текст для поиска");
        }
        else if (IsRegex && RegexMatcher.Validate(Pattern) is { } error)
        {
            errors.Add(error);
        }
    }
}
