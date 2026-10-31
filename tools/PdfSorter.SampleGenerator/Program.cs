using PdfSorter.Core.Rules;
using PdfSorter.SampleGenerator;

// Создаёт демо-набор PDF (счета, договоры, акты, отчёты) и файл правил к нему.
// Использование: dotnet run --project tools/PdfSorter.SampleGenerator -- [папка] (по умолчанию samples)

var outputDirectory = Path.GetFullPath(args.Length > 0 ? args[0] : "samples");

if (Directory.Exists(outputDirectory) && Directory.EnumerateFileSystemEntries(outputDirectory).Any())
{
    Console.WriteLine($"Папка уже существует, пересоздаю: {outputDirectory}");
    Directory.Delete(outputDirectory, recursive: true);
}

Directory.CreateDirectory(outputDirectory);

using var writer = new SamplePdfWriter();
foreach (var document in SampleDocuments.All)
{
    var path = Path.Combine(outputDirectory, document.RelativePath);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);

    var bytes = document.IsCorrupt ? SamplePdfWriter.CreateCorruptFile() : writer.Write(document);
    File.WriteAllBytes(path, bytes);
    File.SetLastWriteTime(path, document.Modified);
    Console.WriteLine($"  {document.RelativePath,-42} {bytes.Length / 1024,5} КБ{(document.IsCorrupt ? "  (повреждён намеренно)" : string.Empty)}");
}

File.WriteAllText(Path.Combine(outputDirectory, "Описание.txt"),
    "Демо-набор PDF Sorter. Этот файл не PDF и программой игнорируется.\n");

var rulesPath = Path.Combine(outputDirectory, "rules.json");
File.WriteAllText(rulesPath, RuleSetStore.Serialize(RuleSet.CreateDefault()));

Console.WriteLine();
Console.WriteLine($"Готово: {SampleDocuments.All.Count} PDF в {outputDirectory}");
Console.WriteLine($"Набор правил: {rulesPath}");
