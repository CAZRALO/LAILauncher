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

    public App()
    {
        // Global Exception Handlers to prevent silent process crashes (0xE0434352)
        DispatcherUnhandledException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[DispatcherUnhandledException] {e.Exception}");
            e.Handled = true; // Prevent app termination
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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Initialize default Theme Resources before UI tree instantiation
            ThemeManager.ApplyTheme(ThemeMode.Linux);

            // 1. Initialize Database
            var connFactory = new SqliteConnectionFactory();
            var dbInit = new DatabaseInitializer(connFactory);
            dbInit.Initialize();

            // 2. Initialize Repositories
            var appRepo = new AppRepository(connFactory);
            var fileRepo = new FileRepository(connFactory);
            var historyRepo = new SearchHistoryRepository(connFactory);
            var settingsRepo = new SettingsRepository(connFactory);

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
            
            // Background pre-cache app icons
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
            
            // Flow Launcher Core Plugins
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

            // Pre-scan bookmarks in background
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
                    try
                    {
                        return Clipboard.GetText();
                    }
                    catch
                    {
                        return string.Empty;
                    }
                },
                ShowNotice = msg =>
                {
                    MessageBox.Show(msg, "Local AI Assistant", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };

            pipeline.RegisterProvider(aiPlugin);

            // 6. Launch Main Window
            _mainWindow = new MainWindow(pipeline, actionRunner, settingsRepo, ollamaProvider, aiPlugin, ollamaProcessService);
            MainWindow = _mainWindow;

            fileIndexer.OnRequestOpenAiWithFile = (prompt, model) =>
            {
                _mainWindow?.Dispatcher.Invoke(() =>
                {
                    _mainWindow.OpenAiStreamingWindow(prompt, model);
                });
            };

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
}
