using System.Collections.Specialized;
using System.Windows;
using PdfSorter.App.ViewModels;

namespace PdfSorter.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        FitToWorkArea();

        // Журнал всегда прокручен к последней записи. Подписка на ItemCollection (а не на коллекцию ViewModel)
        // гарантирует, что ListBox уже обработал добавление, — иначе ScrollIntoView ломает генератор контейнеров.
        ((INotifyCollectionChanged)LogList.Items).CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add && LogList.Items.Count > 0)
            {
                LogList.ScrollIntoView(LogList.Items[^1]);
            }
        };
    }

    /// <summary>На небольших экранах окно не должно вылезать за пределы рабочей области.</summary>
    private void FitToWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(Width, workArea.Width - 40);
        Height = Math.Min(Height, workArea.Height - 40);
    }
}
