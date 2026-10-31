using PdfSorter.Core.Rules;

namespace PdfSorter.Core.Tests.Fakes;

internal static class Candidates
{
    public static readonly DateTime DefaultModified = new(2024, 11, 5, 10, 30, 0);

    public static FileCandidate Create(string fileName, string text = "", DateTime? modified = null) =>
        new(Path.Combine(@"C:\in", fileName), modified ?? DefaultModified, () => text);
}
