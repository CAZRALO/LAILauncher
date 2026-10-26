using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Launcher.AI;
using Launcher.App.Services;

namespace Launcher.App;

public partial class AIStreamingWindow : Window
{
    private readonly IAIProvider _aiProvider;
    private readonly string _prompt;
    private readonly string _model;
    private readonly CancellationTokenSource _cts = new();
    private readonly Stopwatch _stopwatch = new();
    private readonly DispatcherTimer _timer = new();
    private readonly StringBuilder _responseBuilder = new();
    private bool _isStreaming = true;

    public AIStreamingWindow(IAIProvider aiProvider, string prompt, string? model = null)
    {
        InitializeComponent();
        _aiProvider = aiProvider;
        _prompt = prompt;
        _model = !string.IsNullOrWhiteSpace(model) ? model : aiProvider.DefaultModel;

        PromptText.Text = _prompt;
        ModelBadgeText.Text = _model;

        _timer.Interval = TimeSpan.FromMilliseconds(200);
        _timer.Tick += (s, e) =>
        {
            if (_stopwatch.IsRunning)
            {
                TimeElapsedText.Text = $"⏱ {_stopwatch.Elapsed.TotalSeconds:F1}s";
            }
        };

        Loaded += OnLoaded;
        Closing += (s, e) =>
        {
            _cts.Cancel();
            _timer.Stop();
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ThemeManager.EnableWindowBlur(this);
        ResponseTextBox.Text = "Đang kết nối tới Ollama...\n";
        _stopwatch.Start();
        _timer.Start();

        try
        {
            var firstToken = true;
            await foreach (var token in _aiProvider.GenerateStreamAsync(_prompt, _model, _cts.Token))
            {
                if (firstToken)
                {
                    ResponseTextBox.Clear();
                    firstToken = false;
                }

                _responseBuilder.Append(token);
                ResponseTextBox.AppendText(token);
                ResponseScrollViewer.ScrollToEnd();

                var words = _responseBuilder.Length / 5; // heuristic approximation
                TokenEstimateText.Text = $"~{words} từ";
            }

            _isStreaming = false;
            _stopwatch.Stop();
            SetStatusCompleted();
        }
        catch (OperationCanceledException)
        {
            _isStreaming = false;
            _stopwatch.Stop();
            SetStatusStopped();
        }
        catch (Exception ex)
        {
            _isStreaming = false;
            _stopwatch.Stop();
            SetStatusError($"Lỗi kết nối: {ex.Message}\n\nHãy đảm bảo Ollama đang chạy trên máy (mặc định tại {_aiProvider.CurrentEndpoint}).\nKhởi động bằng cách mở Terminal và gõ: ollama run {_model}");
        }
    }

    private void SetStatusCompleted()
    {
        StatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x22, 0x10, 0xB9, 0x81));
        StatusBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0x10, 0xB9, 0x81));
        StatusBadgeText.Text = "Hoàn tất";
        StatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
        StopButton.IsEnabled = false;
    }

    private void SetStatusStopped()
    {
        StatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x22, 0xF5, 0x9E, 0x0B));
        StatusBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0xF5, 0x9E, 0x0B));
        StatusBadgeText.Text = "Đã dừng";
        StatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
        StopButton.IsEnabled = false;
    }

    private void SetStatusError(string errorMessage)
    {
        StatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x22, 0xEF, 0x44, 0x44));
        StatusBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0xEF, 0x44, 0x44));
        StatusBadgeText.Text = "Không thể kết nối";
        StatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
        StopButton.IsEnabled = false;

        ResponseTextBox.AppendText($"\n\n[Lỗi kết nối Ollama]\n{errorMessage}");
    }

    private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private async void OnCopyClicked(object sender, RoutedEventArgs e)
    {
        var text = ResponseTextBox.Text;
        if (!string.IsNullOrEmpty(text))
        {
            Clipboard.SetText(text);
            CopyButton.Content = "✓ Đã sao chép!";
            await Task.Delay(1500);
            CopyButton.Content = "Sao chép (Ctrl+C)";
        }
    }

    private void OnStopClicked(object sender, RoutedEventArgs e)
    {
        if (_isStreaming)
        {
            _cts.Cancel();
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        _cts.Cancel();
        Close();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_isStreaming)
            {
                _cts.Cancel();
                _isStreaming = false;
            }
            else
            {
                Close();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (ResponseTextBox.SelectionLength == 0)
            {
                OnCopyClicked(sender, e);
                e.Handled = true;
            }
        }
    }
}
