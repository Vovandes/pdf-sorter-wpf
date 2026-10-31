namespace PdfSorter.Core.Tests.Fakes;

/// <summary>Предсказуемые часы: каждое обращение сдвигает время на секунду, пакеты журнала упорядочены однозначно.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private static readonly TimeZoneInfo Moscow =
        TimeZoneInfo.CreateCustomTimeZone("Test+03", TimeSpan.FromHours(3), "Test+03", "Test+03");

    private DateTimeOffset _utcNow = new(2025, 3, 20, 9, 0, 0, TimeSpan.Zero);

    public override TimeZoneInfo LocalTimeZone => Moscow;

    public override DateTimeOffset GetUtcNow()
    {
        _utcNow = _utcNow.AddSeconds(1);
        return _utcNow;
    }
}
