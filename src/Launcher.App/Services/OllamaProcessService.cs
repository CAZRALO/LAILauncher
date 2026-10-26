using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Launcher.AI;

namespace Launcher.App.Services;

public sealed record OllamaStartResult(bool Success, string Message);

public sealed class OllamaProcessService
{
    private readonly IAIProvider _provider;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private Process? _process;

    public OllamaProcessService(IAIProvider provider)
    {
        _provider = provider;
    }

    public async Task<OllamaStartResult> StartAsync(CancellationToken cancellationToken = default)
    {
        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (await _provider.CheckHealthAsync())
            {
                return new OllamaStartResult(true, "Ollama đang hoạt động.");
            }

            if (!Uri.TryCreate(_provider.CurrentEndpoint, UriKind.Absolute, out var endpoint) || !endpoint.IsLoopback)
            {
                return new OllamaStartResult(false, "Endpoint hiện tại không phải máy cục bộ; không thể khởi chạy Ollama tại đây.");
            }

            if (_process is { HasExited: false })
            {
                return await WaitForServerAsync(_process, cancellationToken);
            }

            _process?.Dispose();
            _process = Process.Start(new ProcessStartInfo
            {
                FileName = FindOllamaExecutable(),
                Arguments = "serve",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (_process is null)
            {
                return new OllamaStartResult(false, "Không thể khởi chạy tiến trình Ollama.");
            }

            return await WaitForServerAsync(_process, cancellationToken);
        }
        catch (Win32Exception)
        {
            return new OllamaStartResult(false, "Không tìm thấy Ollama. Hãy cài Ollama hoặc thêm ollama.exe vào PATH.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new OllamaStartResult(false, $"Không thể khởi chạy Ollama: {ex.Message}");
        }
        finally
        {
            _startLock.Release();
        }
    }

    private async Task<OllamaStartResult> WaitForServerAsync(Process process, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _provider.CheckHealthAsync())
            {
                return new OllamaStartResult(true, "Ollama đã sẵn sàng.");
            }

            if (process.HasExited)
            {
                return new OllamaStartResult(false, $"Ollama đã dừng với mã lỗi {process.ExitCode}.");
            }

            await Task.Delay(500, cancellationToken);
        }

        return new OllamaStartResult(false, "Ollama chưa phản hồi sau 20 giây. Hãy kiểm tra cài đặt hoặc endpoint.");
    }

    private static string FindOllamaExecutable()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var candidates = new[]
        {
            Path.Combine(localAppData, "Programs", "Ollama", "ollama.exe"),
            Path.Combine(programFiles, "Ollama", "ollama.exe")
        };

        return candidates.FirstOrDefault(File.Exists) ?? "ollama";
    }
}