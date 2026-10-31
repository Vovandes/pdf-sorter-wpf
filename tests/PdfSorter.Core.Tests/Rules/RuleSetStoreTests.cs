using System.IO.Abstractions.TestingHelpers;
using PdfSorter.Core.Rules;

namespace PdfSorter.Core.Tests.Rules;

public class RuleSetStoreTests
{
    [Fact]
    public void SaveAndLoad_PreservesAllRuleTypesAndOrder()
    {
        var fileSystem = new MockFileSystem();
        var store = new RuleSetStore(fileSystem);
        var original = RuleSet.CreateDefault();
        ((PdfTextRule)original.Rules[2]).IsEnabled = false;

        store.Save(@"C:\profiles\правила.json", original);
        var loaded = store.Load(@"C:\profiles\правила.json");

        Assert.Equal(original.FallbackFolder, loaded.FallbackFolder);
        Assert.Equal(original.FileNameTemplate, loaded.FileNameTemplate);
        Assert.Equal(original.Rules.Select(r => r.GetType()), loaded.Rules.Select(r => r.GetType()));
        Assert.Equal(original.Rules.Select(r => r.Name), loaded.Rules.Select(r => r.Name));
        Assert.False(loaded.Rules[2].IsEnabled);

        var contracts = Assert.IsType<PdfTextRule>(loaded.Rules[3]);
        Assert.True(contracts.IsRegex);
        Assert.Equal(((PdfTextRule)original.Rules[3]).Pattern, contracts.Pattern);
        Assert.Equal(DateSource.FileName, Assert.IsType<DateRule>(loaded.Rules[4]).Source);
    }

    [Fact]
    public void Serialize_WritesReadableJsonWithTypeDiscriminator()
    {
        var json = RuleSetStore.Serialize(RuleSet.CreateDefault());

        Assert.Contains("\"type\": \"pdfText\"", json);
        Assert.Contains("\"name\": \"Договоры\"", json); // кириллица без \u-экранирования
        Assert.Contains("\"source\": \"FileName\"", json);
        Assert.DoesNotContain("kindTitle", json); // служебные свойства для UI в файл не попадают
        Assert.DoesNotContain("displayName", json);
    }

    [Theory]
    [InlineData("{ это не json")]
    [InlineData("""{ "rules": [ { "type": "unknown" } ] }""")]
    [InlineData("""{ "version": 99, "rules": [] }""")]
    public void Deserialize_InvalidContent_ThrowsInvalidDataException(string json)
    {
        Assert.Throws<InvalidDataException>(() => RuleSetStore.Deserialize(json));
    }
}
