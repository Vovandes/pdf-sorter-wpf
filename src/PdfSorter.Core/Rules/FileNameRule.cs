using PdfSorter.Core.Matching;

namespace PdfSorter.Core.Rules;

/// <summary>
/// Правило по имени файла: маска с <c>*</c> и <c>?</c> или регулярное выражение.
/// Сначала проверяется имя без расширения, затем полное имя — поэтому подходят и <c>Счет_*</c>, и <c>*.pdf</c>.
/// </summary>
public sealed class FileNameRule : SortRule
{
    public FileNameRule()
        : base("Имя файла")
    {
    }

    /// <summary>Маска (<c>Счет_*</c>) или регулярное выражение (<c>^Счет_(\d+)</c>).</summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary><c>true</c> — <see cref="Pattern"/> является регулярным выражением, иначе маской.</summary>
    public bool IsRegex { get; set; }

    public bool IgnoreCase { get; set; } = true;

    public override RuleMatch? Match(FileCandidate file)
    {
        if (string.IsNullOrWhiteSpace(Pattern))
        {
            return null;
        }

        var regex = IsRegex ? Pattern : WildcardPattern.ToRegex(Pattern);
        foreach (var input in new[] { file.NameWithoutExtension, file.FileName })
        {
            var match = RegexMatcher.Match(input, regex, IgnoreCase);
            if (match.Success)
            {
                return new RuleMatch(RegexMatcher.ExtractValue(match));
            }
        }

        return null;
    }

    public override string DescribeCondition() => IsRegex ? $"/{Pattern}/" : Pattern;

    protected override void ValidateCondition(ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(Pattern))
        {
            errors.Add("не задана маска имени");
        }
        else if (IsRegex && RegexMatcher.Validate(Pattern) is { } error)
        {
            errors.Add(error);
        }
    }
}
