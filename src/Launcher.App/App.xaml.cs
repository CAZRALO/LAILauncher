using System.Windows;
using Launcher.Database;
using Launcher.Database.Repositories;
using Launcher.Core.Search;
using Launcher.Core.Actions;
using Launcher.Indexer;
using Launcher.AI;
using Launcher.App.Services;

namespace Launcher.App;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private TrayIconService? _trayIconService;
    private static Mutex? _singleInstanceMutex;
    private const string MutexName = "Local\\LAILauncher_SingleInstance_Mutex";

    public App()
    {
        DispatcherUnhandledException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[DispatcherUnhandledException] {e.Exception}");
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[AppDomain UnhandledException] {e.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[UnobservedTaskException] {e.Exception}");
            e.SetObserved();
        };
    }

    public void SetTrayIconVisible(bool visible)
    {
        if (_trayIconService == null) return;
        if (visible) _trayIconService.Show();
        else _trayIconService.Hide();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        bool createdNew;
        try
        {
            _singleInstanceMutex = new Mutex(true, MutexName, out createdNew);
        }
        catch
        {
            createdNew = true;
        }

        if (!createdNew)
        {
            Shutdown();
            return;
        }

        Launcher.Core.Utilities.ClipboardHelper.CustomHandler = text =>
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    Clipboard.SetText(text);
                }
                catch { }
            });
        };

        try
        {
            // Initialize default Theme Resources before UI tree instantiation
            ThemeManager.ApplyTheme(ThemeMode.Windows11);

            // 1. Initialize Database
            var connFactory = new SqliteConnectionFactory();
            var dbInit = new DatabaseInitializer(connFactory);
            dbInit.Initialize();

            // 2. Initialize Repositories
            var appRepo = new AppRepository(connFactory);
            var fileRepo = new FileRepository(connFactory);
            var historyRepo = new SearchHistoryRepository(connFactory);
            var settingsRepo = new SettingsRepository(connFactory);

            // Load Language before instantiating UI windows
            var savedLang = await settingsRepo.GetAsync("AppLanguage");
            LocalizationService.SetLanguage(savedLang ?? LocalizationService.Vietnamese);

            // 3. Initialize Indexers & Providers
            var appIndexer = new AppIndexer(appRepo);
            try
            {
                await appIndexer.InitializeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AppIndexer init failed: {ex.Message}");
            }

            var fileIndexer = new FileIndexer(fileRepo);
            try
            {
                await fileIndexer.InitializeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FileIndexer init failed: {ex.Message}");
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var apps = await appRepo.GetAllAsync();
                    IconExtractor.PreCache(apps.Select(a => a.Path));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Icon pre-cache failed: {ex.Message}");
                }
            });

            // 4. Initialize Core Pipeline & Ranking
            var rankingEngine = new RankingEngine(historyRepo);
            var pipeline = new QueryPipeline(rankingEngine);
            pipeline.RegisterProvider(appIndexer);
            pipeline.RegisterProvider(fileIndexer);

            var sysPlugin = new Launcher.Core.Plugins.SystemCommandsPlugin();
            var settingsPlugin = new Launcher.Core.Plugins.WindowsSettingsPlugin();
            var webSearchPlugin = new Launcher.Core.Plugins.WebSearchPlugin();
            var bookmarksPlugin = new Launcher.Core.Plugins.BookmarksPlugin();
            var processKillerPlugin = new Launcher.Core.Plugins.ProcessKillerPlugin();

            pipeline.RegisterProvider(sysPlugin);
            pipeline.RegisterProvider(settingsPlugin);
            pipeline.RegisterProvider(webSearchPlugin);
            pipeline.RegisterProvider(bookmarksPlugin);
            pipeline.RegisterProvider(processKillerPlugin);

            _ = Task.Run(async () =>
            {
                try
                {
                    await bookmarksPlugin.RefreshBookmarksAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Bookmarks pre-scan failed: {ex.Message}");
                }
            });

            var actionRunner = new ActionRunner(historyRepo, appRepo);

            // 5. Initialize AI components
            var savedAiEndpoint = await settingsRepo.GetAsync("AIOllamaEndpoint");
            var savedAiModel = await settingsRepo.GetAsync("AIOllamaModel");

            var ollamaProvider = new OllamaProvider(
                endpoint: !string.IsNullOrWhiteSpace(savedAiEndpoint) ? savedAiEndpoint : "http://localhost:11434",
                defaultModel: !string.IsNullOrWhiteSpace(savedAiModel) ? savedAiModel : "llama3.2:1b"
            );
            var ollamaProcessService = new OllamaProcessService(ollamaProvider);
            var intentClassifier = new IntentClassifier();

            var aiPlugin = new AIPlugin(ollamaProvider, intentClassifier)
            {
                GetClipboardText = () =>
                {
                    try { return Clipboard.GetText(); }
                    catch { return string.Empty; }
                },
                ShowNotice = msg =>
                {
                    MessageBox.Show(msg, "Local AI Assistant", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };

            pipeline.RegisterProvider(aiPlugin);

            // 6. Launch Main Window
            _mainWindow = new MainWindow(
                pipeline,
                actionRunner,
                settingsRepo,
                ollamaProvider,
                aiPlugin,
                ollamaProcessService,
                appIndexer,
                fileIndexer,
                appRepo,
                fileRepo);
            MainWindow = _mainWindow;

            fileIndexer.OnRequestOpenAiWithFile = (prompt, model) =>
            {
                _mainWindow?.Dispatcher.Invoke(() =>
                {
                    _mainWindow.OpenAiStreamingWindow(prompt, model);
                });
            };

            // 7. System Tray Icon Setup
            _trayIconService = new TrayIconService(
                onToggleLauncher: () => _mainWindow?.ToggleVisibility(),
                onOpenSettings: () => _mainWindow?.OpenSettingsWindow(),
                onOpenProcesses: () => _mainWindow?.OpenWithQuery("trinh chay ngam"),
                onSwitchLanguage: async () =>
                {
                    var next = LocalizationService.CurrentLanguage == LocalizationService.English
                        ? LocalizationService.Vietnamese
                        : LocalizationService.English;
                    LocalizationService.SetLanguage(next);
                    await settingsRepo.SetAsync("AppLanguage", next);
                },
                onExit: () => Shutdown());

            var showTrayStr = await settingsRepo.GetAsync("AppShowTrayIcon");
            bool showTray = string.IsNullOrWhiteSpace(showTrayStr) || (bool.TryParse(showTrayStr, out var st) && st);
            if (showTray)
            {
                _trayIconService.Show();
            }

            _mainWindow.Show();

            var startOllamaWithApp = await settingsRepo.GetAsync("AIOllamaStartWithApp");
            if (bool.TryParse(startOllamaWithApp, out var shouldStartOllama) && shouldStartOllama)
            {
                _ = Task.Run(async () =>
                {
                    var result = await ollamaProcessService.StartAsync();
                    System.Diagnostics.Debug.WriteLine($"Ollama startup: {result.Message}");
                });
            }
        }
        catch (Exception fatalEx)
        {
            System.Diagnostics.Debug.WriteLine($"[Fatal Startup Error] {fatalEx}");
            MessageBox.Show($"Startup Error: {fatalEx.Message}", "LAILauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _trayIconService?.Dispose();
        }
        catch { }

        try
        {
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
        }
        catch { }

        base.OnExit(e);
    }
}
