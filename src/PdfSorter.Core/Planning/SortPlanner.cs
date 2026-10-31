using System.IO.Abstractions;
using PdfSorter.Core.IO;
using PdfSorter.Core.Naming;
using PdfSorter.Core.Pdf;
using PdfSorter.Core.Rules;

namespace PdfSorter.Core.Planning;

/// <summary>
/// Строит план сортировки, ничего не меняя на диске: находит PDF-файлы, прогоняет их через правила
/// (первое подошедшее срабатывает), вычисляет новые пути и разрешает конфликты имён.
/// </summary>
public sealed class SortPlanner
{
    /// <summary>Ключ счётчика <c>{counter}</c> для файлов, попавших в папку «прочее».</summary>
    private const int FallbackCounterKey = -1;

    /// <summary>Название «правила» для файлов, не подошедших ни под одно правило.</summary>
    public const string FallbackRuleName = "(прочее)";

    private readonly IFileSystem _fileSystem;
    private readonly IPdfTextReader _pdfTextReader;

    public SortPlanner(IFileSystem fileSystem, IPdfTextReader pdfTextReader)
    {
        _fileSystem = fileSystem;
        _pdfTextReader = pdfTextReader;
    }

    /// <exception cref="SortConfigurationException">Не выбраны папки или правила настроены с ошибками.</exception>
    /// <exception cref="OperationCanceledException">Построение плана отменено.</exception>
    public SortPlan BuildPlan(SortJob job, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        Validate(job);

        var normalizedJob = job with
        {
            SourceFolder = _fileSystem.Path.GetFullPath(job.SourceFolder),
            TargetFolder = _fileSystem.Path.GetFullPath(job.TargetFolder),
        };

        var files = FindPdfFiles(normalizedJob);
        var state = new PlanningState();
        var items = new List<PlanItem>(files.Count);
        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            items.Add(PlanFile(path, normalizedJob, state));
            progress?.Report(items.Count * 100 / files.Count);
        }

        return new SortPlan(normalizedJob, items);
    }

    private void Validate(SortJob job)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(job.SourceFolder))
        {
            errors.Add("Не выбрана исходная папка.");
        }
        else if (!_fileSystem.Directory.Exists(job.SourceFolder))
        {
            errors.Add($"Исходная папка не найдена: {job.SourceFolder}");
        }

        if (string.IsNullOrWhiteSpace(job.TargetFolder))
        {
            errors.Add("Не выбрана целевая папка.");
        }

        var ruleSet = job.RuleSet;
        errors.AddRange(PathTemplate.Validate(ruleSet.FileNameTemplate).Select(e => $"Общий шаблон имени: {e}."));
        errors.AddRange(PathTemplate.Validate(ruleSet.FallbackFolder).Select(e => $"Папка для прочих файлов: {e}."));

        var enabledRules = ruleSet.Rules.Where(r => r.IsEnabled).ToList();
        if (enabledRules.Count == 0 && string.IsNullOrWhiteSpace(ruleSet.FallbackFolder))
        {
            errors.Add("Нет ни одного включённого правила.");
        }

        foreach (var rule in enabledRules)
        {
            errors.AddRange(rule.Validate().Select(e => $"Правило «{rule.DisplayName}»: {e}."));
        }

        if (errors.Count > 0)
        {
            throw new SortConfigurationException(errors);
        }
    }

    private List<string> FindPdfFiles(SortJob job)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = job.IncludeSubfolders,
            IgnoreInaccessible = true,
        };

        // Если целевая папка лежит внутри исходной, уже разложенные файлы повторно не берём.
        var skipTarget = job.IncludeSubfolders
            && !PathUtil.AreSame(job.SourceFolder, job.TargetFolder)
            && PathUtil.IsSameOrInside(job.TargetFolder, job.SourceFolder);

        return _fileSystem.Directory.EnumerateFiles(job.SourceFolder, "*", options)
            .Where(path => string.Equals(_fileSystem.Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
            .Where(path => !skipTarget || !PathUtil.IsSameOrInside(path, job.TargetFolder))
            .OrderBy(path => path, PathUtil.Comparer)
            .ToList();
    }

    private PlanItem PlanFile(string sourcePath, SortJob job, PlanningState state)
    {
        SortRule? currentRule = null;
        try
        {
            var candidate = new FileCandidate(
                sourcePath,
                _fileSystem.File.GetLastWriteTime(sourcePath),
                () => _pdfTextReader.ReadFirstPageText(sourcePath));

            var rules = job.RuleSet.Rules;
            for (var index = 0; index < rules.Count; index++)
            {
                currentRule = rules[index];
                if (currentRule.IsEnabled && currentRule.Match(candidate) is { } match)
                {
                    var nameTemplate = string.IsNullOrWhiteSpace(currentRule.FileNameTemplate)
                        ? job.RuleSet.FileNameTemplate
                        : currentRule.FileNameTemplate;
                    return PlanDestination(
                        candidate, job, state, index, currentRule.DisplayName,
                        currentRule.TargetFolder, nameTemplate, match);
                }
            }

            currentRule = null;
            if (!string.IsNullOrWhiteSpace(job.RuleSet.FallbackFolder))
            {
                return PlanDestination(
                    candidate, job, state, FallbackCounterKey, FallbackRuleName,
                    job.RuleSet.FallbackFolder, job.RuleSet.FileNameTemplate, new RuleMatch());
            }

            return new PlanItem
            {
                SourcePath = sourcePath,
                Status = PlanItemStatus.Skipped,
                Message = "ни одно правило не подошло",
            };
        }
        catch (Exception ex) when (ErrorDescriber.IsFileLevel(ex))
        {
            // RuleName — правило, на проверке которого произошла ошибка (например, чтение текста PDF).
            return new PlanItem
            {
                SourcePath = sourcePath,
                RuleName = currentRule?.DisplayName,
                Status = PlanItemStatus.Error,
                Message = ErrorDescriber.Describe(ex),
            };
        }
    }

    private PlanItem PlanDestination(
        FileCandidate candidate,
        SortJob job,
        PlanningState state,
        int counterKey,
        string ruleName,
        string folderTemplate,
        string nameTemplate,
        RuleMatch match)
    {
        var context = new TemplateContext(
            candidate.NameWithoutExtension,
            candidate.Extension,
            match.Date ?? candidate.DateFromName ?? candidate.LastWriteTime,
            state.PeekCounter(counterKey),
            match.Value,
            ruleName);

        var folder = PathTemplate.RenderFolder(folderTemplate, context);
        var fileName = PathTemplate.RenderFileName(nameTemplate, context);
        var desired = _fileSystem.Path.Combine(job.TargetFolder, folder, fileName);

        var destination = UniquePathResolver.Resolve(desired, path =>
            state.IsPlanned(path)
            || (!PathUtil.AreSame(path, candidate.FullPath) && _fileSystem.File.Exists(path)));

        if (PathUtil.AreSame(destination, candidate.FullPath))
        {
            return new PlanItem
            {
                SourcePath = candidate.FullPath,
                DestinationPath = destination,
                RuleName = ruleName,
                Status = PlanItemStatus.Skipped,
                Message = "файл уже на своём месте",
            };
        }

        state.Reserve(destination, counterKey);
        return new PlanItem
        {
            SourcePath = candidate.FullPath,
            DestinationPath = destination,
            RuleName = ruleName,
            Status = PlanItemStatus.Ready,
            Message = PathUtil.AreSame(destination, desired) ? null : UniquePathResolver.ConflictMessage,
        };
    }

    /// <summary>Пути, уже занятые планом, и счётчики <c>{counter}</c> по правилам.</summary>
    private sealed class PlanningState
    {
        private readonly HashSet<string> _plannedPaths = new(PathUtil.Comparer);
        private readonly Dictionary<int, int> _counters = [];

        public bool IsPlanned(string path) => _plannedPaths.Contains(path);

        public int PeekCounter(int key) => _counters.GetValueOrDefault(key) + 1;

        public void Reserve(string path, int counterKey)
        {
            _plannedPaths.Add(path);
            _counters[counterKey] = PeekCounter(counterKey);
        }
    }
}
