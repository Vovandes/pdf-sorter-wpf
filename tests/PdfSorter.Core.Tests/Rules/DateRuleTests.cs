using PdfSorter.Core.Matching;
using PdfSorter.Core.Naming;
using PdfSorter.Core.Rules;
using PdfSorter.Core.Tests.Fakes;

namespace PdfSorter.Core.Tests.Rules;

public class DateRuleTests
{
    [Theory]
    [InlineData("Счет_2025-03-14", 2025, 3, 14)]
    [InlineData("Акт 14.03.2025", 2025, 3, 14)]
    [InlineData("scan_2025_12_01_final", 2025, 12, 1)]
    [InlineData("14-03-2025 договор", 2025, 3, 14)]
    [InlineData("2024-02-30 неверная, 2024-02-29 верная", 2024, 2, 29)] // первая некорректная дата пропускается
    public void Parser_ExtractsDateFromName(string name, int year, int month, int day)
    {
        Assert.Equal(new DateTime(year, month, day), FileNameDateParser.TryParse(name));
    }

    [Theory]
    [InlineData("Счет_145")]
    [InlineData("2025-13-45")]       // нет такого месяца и дня
    [InlineData("12025-03-140")]     // часть более длинного числа
    [InlineData("2025-03.14")]       // разные разделители
    [InlineData("")]
    public void Parser_ReturnsNullWithoutValidDate(string name)
    {
        Assert.Null(FileNameDateParser.TryParse(name));
    }

    [Fact]
    public void FileNameSource_UsesDateFromName()
    {
        var rule = new DateRule { Source = DateSource.FileName };

        var match = rule.Match(Candidates.Create("Отчет 2025-07-10.pdf"));

        Assert.Equal(new DateTime(2025, 7, 10), match?.Date);
    }

    [Fact]
    public void FileNameSource_DoesNotMatchWithoutDateInName()
    {
        var rule = new DateRule { Source = DateSource.FileName };

        Assert.Null(rule.Match(Candidates.Create("Памятка.pdf")));
    }

    [Fact]
    public void FileNameOrModifiedSource_FallsBackToModificationDate()
    {
        var modified = new DateTime(2025, 6, 30, 18, 45, 0);
        var rule = new DateRule { Source = DateSource.FileNameOrLastModified };

        var match = rule.Match(Candidates.Create("Памятка.pdf", modified: modified));

        Assert.Equal(modified.Date, match?.Date);
    }

    [Fact]
    public void LastModifiedSource_IgnoresDateInName()
    {
        var modified = new DateTime(2023, 1, 9);
        var rule = new DateRule { Source = DateSource.LastModified };

        var match = rule.Match(Candidates.Create("Отчет 2025-07-10.pdf", modified: modified));

        Assert.Equal(modified, match?.Date);
    }

    [Fact]
    public void DefaultFolderTemplate_LaysOutByYearAndMonth()
    {
        var rule = new DateRule();
        var context = new TemplateContext("Отчет", "pdf", new DateTime(2025, 3, 14));

        Assert.Equal(Path.Combine("2025", "03"), PathTemplate.RenderFolder(rule.TargetFolder, context));
    }
}
