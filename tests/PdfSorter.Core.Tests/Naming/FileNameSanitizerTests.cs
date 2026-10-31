using PdfSorter.Core.Naming;

namespace PdfSorter.Core.Tests.Naming;

public class FileNameSanitizerTests
{
    [Theory]
    [InlineData("Счет: №145 <копия>.pdf", "Счет_ №145 _копия_.pdf")]
    [InlineData("a/b\\c|d?e*f\"g.pdf", "a_b_c_d_e_f_g.pdf")]
    [InlineData("  много   пробелов  .pdf", "много пробелов.pdf")]
    [InlineData("точка в конце....pdf", "точка в конце.pdf")]
    [InlineData("CON.pdf", "_CON.pdf")]
    [InlineData("nul.pdf", "_nul.pdf")]
    [InlineData("console.pdf", "console.pdf")] // похоже, но не зарезервировано
    [InlineData(".pdf", "_.pdf")]
    [InlineData("табуляция\tи\nперенос.pdf", "табуляция_и_перенос.pdf")]
    public void SanitizeFileName_ProducesValidWindowsName(string input, string expected)
    {
        Assert.Equal(expected, FileNameSanitizer.SanitizeFileName(input));
    }

    [Theory]
    [InlineData("..", "_")]
    [InlineData(".", "_")]
    [InlineData("", "_")]
    [InlineData("Отчеты.", "Отчеты")]
    [InlineData("LPT1", "_LPT1")]
    public void SanitizeSegment_NeutralizesSpecialNames(string input, string expected)
    {
        Assert.Equal(expected, FileNameSanitizer.SanitizeSegment(input));
    }

    [Fact]
    public void SanitizeFileName_TruncatesLongNamesKeepingExtension()
    {
        var result = FileNameSanitizer.SanitizeFileName(new string('я', 500) + ".pdf");

        Assert.Equal(FileNameSanitizer.MaxFileNameLength, result.Length);
        Assert.EndsWith("я.pdf", result);
    }
}
