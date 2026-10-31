namespace PdfSorter.Core.Planning;

/// <summary>План сортировки: строится заранее, показывается в предпросмотре и затем исполняется.</summary>
public sealed class SortPlan
{
    public SortPlan(SortJob job, IReadOnlyList<PlanItem> items)
    {
        Job = job;
        Items = items;
    }

    public SortJob Job { get; }

    public IReadOnlyList<PlanItem> Items { get; }

    public int ReadyCount => Items.Count(i => i.Status == PlanItemStatus.Ready);

    public int SkippedCount => Items.Count(i => i.Status == PlanItemStatus.Skipped);

    public int ErrorCount => Items.Count(i => i.Status == PlanItemStatus.Error);
}
