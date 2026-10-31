using PdfSorter.Core.Naming;

namespace PdfSorter.Core.Tests.Naming;

public class UniquePathResolverTests
{
    [Fact]
    public void FreePath_IsReturnedAsIs()
    {
        var path = @"C:\out\Договор.pdf";

        Assert.Equal(path, UniquePathResolver.Resolve(path, _ => false));
    }

    [Fact]
    public void TakenPath_GetsFirstFreeNumericSuffix()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\out\Договор.pdf",
            @"C:\out\Договор (2).pdf",
        };

        var result = UniquePathResolver.Resolve(@"C:\out\договор.pdf", taken.Contains);

        Assert.Equal(@"C:\out\договор (3).pdf", result);
    }
}
