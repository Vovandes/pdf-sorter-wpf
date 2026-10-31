namespace PdfSorter.Core.Rules;

/// <summary>Результат срабатывания правила.</summary>
/// <param name="Value">Значение для подстановки <c>{match}</c>: группа regex, найденное ключевое слово и т.п.</param>
/// <param name="Date">Дата, выбранная правилом (у правил по дате), иначе <c>null</c>.</param>
public sealed record RuleMatch(string? Value = null, DateTime? Date = null);
