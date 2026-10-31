namespace PdfSorter.Core.Rules;

/// <summary>Откуда правило по дате берёт дату.</summary>
public enum DateSource
{
    /// <summary>Только из имени файла; если даты в имени нет — правило не срабатывает.</summary>
    FileName,

    /// <summary>Дата изменения файла.</summary>
    LastModified,

    /// <summary>Из имени файла, а если её там нет — дата изменения.</summary>
    FileNameOrLastModified,
}

/// <summary>Правило по дате: раскладывает файлы по папкам вида <c>{yyyy}/{MM}</c>.</summary>
public sealed class DateRule : SortRule
{
    public DateRule()
        : base("Дата")
    {
        TargetFolder = "{yyyy}/{MM}";
    }

    public DateSource Source { get; set; } = DateSource.FileName;

    public override RuleMatch? Match(FileCandidate file)
    {
        DateTime? date = Source switch
        {
            DateSource.FileName => file.DateFromName,
            DateSource.LastModified => file.LastWriteTime,
            DateSource.FileNameOrLastModified => file.DateFromName ?? file.LastWriteTime,
            _ => null,
        };

        return date is { } value
            ? new RuleMatch(value.ToString("yyyy-MM-dd"), value.Date)
            : null;
    }

    public override string DescribeCondition() => Source switch
    {
        DateSource.FileName => "из имени файла",
        DateSource.LastModified => "дата изменения",
        _ => "из имени, иначе дата изменения",
    };

    protected override void ValidateCondition(ICollection<string> errors)
    {
        if (!Enum.IsDefined(Source))
        {
            errors.Add("неизвестный источник даты");
        }
    }
}
