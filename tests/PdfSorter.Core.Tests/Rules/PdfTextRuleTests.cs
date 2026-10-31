using PdfSorter.Core.Rules;
using PdfSorter.Core.Tests.Fakes;

namespace PdfSorter.Core.Tests.Rules;

public class PdfTextRuleTests
{
    private const string ContractText = "ДОГОВОР ПОСТАВКИ № Д-2025/017 г. Москва «20» января 2025 г.";

    [Fact]
    public void Keyword_FoundIgnoringCaseAndExtraSpaces()
    {
        var rule = new PdfTextRule { Pattern = "акт  выполненных работ" };

        var match = rule.Match(Candidates.Create("scan.pdf", "АКТ ВЫПОЛНЕННЫХ\nРАБОТ № 7 от 28.02.2025"));

        Assert.NotNull(match);
    }

    [Fact]
    public void Keyword_NotFound_ReturnsNull()
    {
        var rule = new PdfTextRule { Pattern = "Акт выполненных работ" };

        Assert.Null(rule.Match(Candidates.Create("scan.pdf", "АКТ СВЕРКИ ВЗАИМНЫХ РАСЧЕТОВ")));
    }

    [Fact]
    public void Regex_ExtractsContractNumberFromGroup()
    {
        var rule = new PdfTextRule { Pattern = @"Договор[^№]{0,40}№\s*([\w\-/]+)", IsRegex = true };

        var match = rule.Match(Candidates.Create("scan.pdf", ContractText));

        Assert.Equal("Д-2025/017", match?.Value);
    }

    [Fact]
    public void Regex_WithoutGroup_UsesWholeMatch()
    {
        var rule = new PdfTextRule { Pattern = @"Д-\d{4}/\d+", IsRegex = true };

        Assert.Equal("Д-2025/017", rule.Match(Candidates.Create("scan.pdf", ContractText))?.Value);
    }

    [Fact]
    public void TextIsReadOnlyOnceAndOnlyWhenNeeded()
    {
        var reads = 0;
        var candidate = new FileCandidate(@"C:\in\scan.pdf", DateTime.Now, () =>
        {
            reads++;
            return ContractText;
        });
        var nameRule = new FileNameRule { Pattern = "scan*" };
        var textRules = new[]
        {
            new PdfTextRule { Pattern = "акт" },
            new PdfTextRule { Pattern = "договор" },
        };

        Assert.NotNull(nameRule.Match(candidate));
        Assert.Equal(0, reads);

        Assert.Null(textRules[0].Match(candidate));
        Assert.NotNull(textRules[1].Match(candidate));
        Assert.Equal(1, reads);
    }
}
