namespace PdfSorter.Core.Planning;

/// <summary>Задание нельзя выполнить: не выбраны папки или правила настроены с ошибками.</summary>
public sealed class SortConfigurationException : Exception
{
    public SortConfigurationException(IReadOnlyList<string> errors)
        : base(string.Join(Environment.NewLine, errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
