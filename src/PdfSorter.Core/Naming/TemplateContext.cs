namespace PdfSorter.Core.Naming;

/// <summary>Значения для подстановки в шаблоны папок и имён.</summary>
/// <param name="Name">Исходное имя файла без расширения — <c>{name}</c>.</param>
/// <param name="Extension">Расширение без точки — <c>{ext}</c>.</param>
/// <param name="Date">Дата документа — <c>{date:формат}</c>, <c>{yyyy}</c>, <c>{MM}</c>, <c>{dd}</c>.</param>
/// <param name="Counter">Порядковый номер файла среди сработавших по тому же правилу — <c>{counter:000}</c>.</param>
/// <param name="Match">Значение, извлечённое правилом — <c>{match}</c>.</param>
/// <param name="RuleName">Название сработавшего правила — <c>{rule}</c>.</param>
public sealed record TemplateContext(
    string Name,
    string Extension,
    DateTime Date,
    int Counter = 1,
    string? Match = null,
    string? RuleName = null);
