using CommunityToolkit.Mvvm.ComponentModel;
using PdfSorter.Core.Rules;

namespace PdfSorter.App.ViewModels;

/// <summary>Пара «значение — подпись» для выпадающих списков.</summary>
public sealed record Option<T>(T Value, string Title);

/// <summary>
/// Обёртка над правилом ядра для редактирования. Свойства пишут прямо в модель,
/// поэтому набор правил для планировщика собирается без копирования полей.
/// </summary>
public abstract class RuleViewModel : ObservableObject
{
    private int _number;

    protected RuleViewModel(SortRule model)
    {
        Model = model;
    }

    public SortRule Model { get; }

    public int Number
    {
        get => _number;
        set => SetProperty(ref _number, value);
    }

    public string KindTitle => Model.KindTitle;

    public string DisplayName => Model.DisplayName;

    /// <summary>Условие правила коротко: маска, выражение или ключевое слово.</summary>
    public string Description => Model.DescribeCondition();

    /// <summary>Ошибки настройки правила одной строкой или <c>null</c>.</summary>
    public string? ValidationError => Model.Validate() is { Count: > 0 } errors ? string.Join("; ", errors) : null;

    public string Name
    {
        get => Model.Name;
        set => SetModel(Model.Name, value, (m, v) => m.Name = v);
    }

    public bool IsEnabled
    {
        get => Model.IsEnabled;
        set => SetModel(Model.IsEnabled, value, (m, v) => m.IsEnabled = v);
    }

    public string TargetFolder
    {
        get => Model.TargetFolder;
        set => SetModel(Model.TargetFolder, value, (m, v) => m.TargetFolder = v);
    }

    public string FileNameTemplate
    {
        get => Model.FileNameTemplate ?? string.Empty;
        set => SetModel(Model.FileNameTemplate ?? string.Empty, value,
            (m, v) => m.FileNameTemplate = string.IsNullOrWhiteSpace(v) ? null : v);
    }

    public static RuleViewModel Create(SortRule rule) => rule switch
    {
        FileNameRule fileNameRule => new FileNameRuleViewModel(fileNameRule),
        DateRule dateRule => new DateRuleViewModel(dateRule),
        PdfTextRule textRule => new PdfTextRuleViewModel(textRule),
        _ => throw new NotSupportedException($"Неизвестный тип правила: {rule.GetType().Name}"),
    };

    /// <summary>Меняет поле модели и обновляет производные свойства (название, описание, ошибки).</summary>
    protected bool SetModel<T>(T oldValue, T newValue, Action<SortRule, T> setter, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(oldValue, newValue, Model, setter, propertyName))
        {
            return false;
        }

        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(ValidationError));
        return true;
    }
}

public sealed class FileNameRuleViewModel : RuleViewModel
{
    public FileNameRuleViewModel(FileNameRule model)
        : base(model)
    {
    }

    private FileNameRule Rule => (FileNameRule)Model;

    public string Pattern
    {
        get => Rule.Pattern;
        set => SetModel(Rule.Pattern, value, (m, v) => ((FileNameRule)m).Pattern = v);
    }

    public bool IsRegex
    {
        get => Rule.IsRegex;
        set
        {
            if (SetModel(Rule.IsRegex, value, (m, v) => ((FileNameRule)m).IsRegex = v))
            {
                OnPropertyChanged(nameof(IsWildcard));
            }
        }
    }

    /// <summary>Для переключателя «маска / регулярное выражение».</summary>
    public bool IsWildcard
    {
        get => !IsRegex;
        set => IsRegex = !value;
    }

    public bool IgnoreCase
    {
        get => Rule.IgnoreCase;
        set => SetModel(Rule.IgnoreCase, value, (m, v) => ((FileNameRule)m).IgnoreCase = v);
    }
}

public sealed class DateRuleViewModel : RuleViewModel
{
    public DateRuleViewModel(DateRule model)
        : base(model)
    {
    }

    public static IReadOnlyList<Option<DateSource>> SourceOptions { get; } =
    [
        new(DateSource.FileName, "Из имени файла (2025-03-14, 14.03.2025)"),
        new(DateSource.FileNameOrLastModified, "Из имени, иначе дата изменения"),
        new(DateSource.LastModified, "Дата изменения файла"),
    ];

    private DateRule Rule => (DateRule)Model;

    public DateSource Source
    {
        get => Rule.Source;
        set => SetModel(Rule.Source, value, (m, v) => ((DateRule)m).Source = v);
    }
}

public sealed class PdfTextRuleViewModel : RuleViewModel
{
    public PdfTextRuleViewModel(PdfTextRule model)
        : base(model)
    {
    }

    private PdfTextRule Rule => (PdfTextRule)Model;

    public string Pattern
    {
        get => Rule.Pattern;
        set => SetModel(Rule.Pattern, value, (m, v) => ((PdfTextRule)m).Pattern = v);
    }

    public bool IsRegex
    {
        get => Rule.IsRegex;
        set
        {
            if (SetModel(Rule.IsRegex, value, (m, v) => ((PdfTextRule)m).IsRegex = v))
            {
                OnPropertyChanged(nameof(IsKeyword));
            }
        }
    }

    /// <summary>Для переключателя «ключевое слово / регулярное выражение».</summary>
    public bool IsKeyword
    {
        get => !IsRegex;
        set => IsRegex = !value;
    }

    public bool IgnoreCase
    {
        get => Rule.IgnoreCase;
        set => SetModel(Rule.IgnoreCase, value, (m, v) => ((PdfTextRule)m).IgnoreCase = v);
    }
}
