using PdfSorter.Core.Rules;
using PdfSorter.Core.Tests.Fakes;

namespace PdfSorter.Core.Tests.Rules;

public class FileNameRuleTests
{
    [Theory]
    [InlineData("Счет_*", "Счет_2025-03-14.pdf", true)]
    [InlineData("Счет_*", "счет_145.pdf", true)] // без учёта регистра по умолчанию
    [InlineData("Счет_*", "Договор_1.pdf", false)]
    [InlineData("*.pdf", "что угодно.pdf", true)]
    [InlineData("scan_????", "scan_0012.pdf", true)]
    [InlineData("scan_????", "scan_012.pdf", false)]
    [InlineData("Акт*2025*", "Акт сверки 31.03.2025.pdf", true)]
    [InlineData("a+b(1)", "a+b(1).pdf", true)] // спецсимволы regex в маске экранируются
    public void Wildcard_MatchesFileName(string mask, string fileName, bool expected)
    {
        var rule = new FileNameRule { Pattern = mask };

        Assert.Equal(expected, rule.Match(Candidates.Create(fileName)) is not null);
    }

    [Fact]
    public void Wildcard_StarValueGoesToMatch()
    {
        var rule = new FileNameRule { Pattern = "Счет_*" };

        var match = rule.Match(Candidates.Create("Счет_Ромашка.pdf"));

        Assert.Equal("Ромашка", match?.Value);
    }

    [Fact]
    public void Wildcard_RespectsCaseSensitivityFlag()
    {
        var rule = new FileNameRule { Pattern = "Счет_*", IgnoreCase = false };

        Assert.Null(rule.Match(Candidates.Create("счет_1.pdf")));
        Assert.NotNull(rule.Match(Candidates.Create("Счет_1.pdf")));
    }

    [Theory]
    [InlineData(@"^Сч[её]т\D*(\d+)", "Счет №145 от 14.03.2025.pdf", "145")]
    [InlineData(@"^Сч[её]т\D*(\d+)", "Счёт 160 от 2025-04-02.pdf", "160")]
    [InlineData(@"_(\d{4})$", "report_2025.pdf", "2025")] // проверяется имя без расширения
    [InlineData(@"\.pdf$", "report.pdf", ".pdf")]         // и полное имя
    public void Regex_ExtractsFirstGroup(string pattern, string fileName, string expected)
    {
        var rule = new FileNameRule { Pattern = pattern, IsRegex = true };

        Assert.Equal(expected, rule.Match(Candidates.Create(fileName))?.Value);
    }

    [Fact]
    public void Regex_NoMatch_ReturnsNull()
    {
        var rule = new FileNameRule { Pattern = @"^Договор", IsRegex = true };

        Assert.Null(rule.Match(Candidates.Create("Счет.pdf")));
    }

    [Theory]
    [InlineData("", false, "не задана маска")]
    [InlineData("Счет(", true, "некорректное регулярное выражение")]
    public void Validate_ReportsInvalidPattern(string pattern, bool isRegex, string expectedFragment)
    {
        var rule = new FileNameRule { Pattern = pattern, IsRegex = isRegex };

        Assert.Contains(rule.Validate(), error => error.Contains(expectedFragment));
    }

    [Fact]
    public void Validate_ReportsUnknownPlaceholderInTemplates()
    {
        var rule = new FileNameRule { Pattern = "*", TargetFolder = "{year}", FileNameTemplate = "{nmae}" };

        var errors = rule.Validate();

        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.StartsWith("папка") && e.Contains("{year}"));
        Assert.Contains(errors, e => e.StartsWith("имя файла") && e.Contains("{nmae}"));
    }
}
