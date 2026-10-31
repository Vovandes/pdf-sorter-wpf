using System.IO;
using System.IO.Abstractions;
using System.Windows;
using System.Windows.Threading;
using PdfSorter.App.Services;
using PdfSorter.App.ViewModels;
using PdfSorter.Core.Execution;
using PdfSorter.Core.Journal;
using PdfSorter.Core.Pdf;
using PdfSorter.Core.Planning;
using PdfSorter.Core.Rules;

namespace PdfSorter.App;

/// <summary>Точка входа: собирает зависимости вручную (их немного) и при необходимости запускает сценарий из командной строки.</summary>
public partial class App : Application
{
    private bool _isAutomation;
    private bool _isReportingError;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        StartupOptions options;
        try
        {
            options = StartupOptions.Parse(e.Args);
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show($"{ex.Message}\n\n{StartupOptions.Usage}", "PDF Sorter", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(2);
            return;
        }

        _isAutomation = options.IsAutomation;
        var paths = AppPaths.Resolve(options.DataDirectory);
        var fileSystem = new FileSystem();
        var journal = new JsonJournalStore(fileSystem, paths.LogsDirectory);
        IDialogService dialogs = options.IsAutomation ? new AutomationDialogService() : new WpfDialogService();
        var settingsStore = new AppSettingsStore(fileSystem, paths.SettingsFile);

        var viewModel = new MainViewModel(
            new SortPlanner(fileSystem, new PdfPigTextReader(fileSystem)),
            new SortExecutor(fileSystem, journal),
            new UndoService(fileSystem, journal),
            new RuleSetStore(fileSystem),
            dialogs,
            journal.Location);

        try
        {
            viewModel.ApplySettings(options.IsAutomation ? new AppSettings() : settingsStore.Load());
            ApplyOptions(viewModel, options);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            MessageBox.Show(ex.Message, "PDF Sorter", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(3);
            return;
        }

        var window = new MainWindow(viewModel);
        if (options.WindowWidth is { } width)
        {
            window.Width = width;
        }

        if (options.WindowHeight is { } height)
        {
            window.Height = height;
        }

        if (!options.IsAutomation)
        {
            window.Closing += (_, _) => settingsStore.Save(viewModel.CaptureSettings());
        }

        MainWindow = window;
        window.Show();

        if (options.IsAutomation)
        {
            await RunAutomationAsync(window, viewModel, options);
        }
    }

    private static void ApplyOptions(MainViewModel viewModel, StartupOptions options)
    {
        if (options.RulesFile is { } rulesFile)
        {
            viewModel.LoadRulesFromFile(Path.GetFullPath(rulesFile));
        }

        if (options.SourceFolder is { } source)
        {
            viewModel.SourceFolder = Path.GetFullPath(source);
        }

        if (options.TargetFolder is { } target)
        {
            viewModel.TargetFolder = Path.GetFullPath(target);
        }

        if (options.IncludeSubfolders is { } includeSubfolders)
        {
            viewModel.IncludeSubfolders = includeSubfolders;
        }

        if (options.Mode is { } mode)
        {
            viewModel.Mode = mode;
        }

        if (options.SelectedRule is { } number && number <= viewModel.Rules.Count)
        {
            viewModel.SelectedRule = viewModel.Rules[number - 1];
        }
    }

    /// <summary>Выполняет действие из <c>--stage</c> и, если задан <c>--screenshot</c>, сохраняет снимок окна и закрывается.</summary>
    private async Task RunAutomationAsync(MainWindow window, MainViewModel viewModel, StartupOptions options)
    {
        await WaitForIdleAsync(window);

        var command = options.Stage switch
        {
            AutomationStage.Preview => viewModel.PreviewCommand,
            AutomationStage.DryRun => viewModel.DryRunCommand,
            AutomationStage.Run => viewModel.ExecuteCommand,
            AutomationStage.Undo => viewModel.UndoCommand,
            _ => null,
        };

        if (command is not null)
        {
            await command.ExecuteAsync(null);
        }

        if (options.ScreenshotPath is { } screenshotPath)
        {
            await WaitForIdleAsync(window);
            ScreenshotRenderer.SaveToPng(window, screenshotPath, options.ScreenshotScale);
            Shutdown(0);
        }
    }

    /// <summary>Ждёт, пока окно отрисуется (DataGrid создаёт строки асинхронно).</summary>
    private static async Task WaitForIdleAsync(Window window)
    {
        for (var i = 0; i < 3; i++)
        {
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            await Task.Delay(150);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Console.Error.WriteLine(e.Exception);

        // MessageBox прокачивает очередь сообщений: если ошибка повторяется (например, при разметке),
        // без этой защиты окна с ошибкой открывались бы друг из друга до переполнения стека.
        if (_isReportingError)
        {
            return;
        }

        if (_isAutomation)
        {
            Shutdown(1);
            return;
        }

        _isReportingError = true;
        try
        {
            MessageBox.Show(
                $"Непредвиденная ошибка: {e.Exception.Message}\n\nПриложение продолжит работу, но текущая операция могла не завершиться.",
                "PDF Sorter", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isReportingError = false;
        }
    }
}
