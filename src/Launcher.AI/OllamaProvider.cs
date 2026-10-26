using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Launcher.AI;

public class OllamaProvider : IAIProvider
{
    private readonly HttpClient _httpClient;
    private string _endpoint;
    private string _defaultModel;

    public string ProviderName => "Ollama";
    public bool IsAvailable { get; private set; }
    public string CurrentEndpoint => _endpoint;
    public string DefaultModel
    {
        get => _defaultModel;
        set => _defaultModel = string.IsNullOrWhiteSpace(value) ? "llama3.2:1b" : value.Trim();
    }

    public OllamaProvider(string endpoint = "http://localhost:11434", string defaultModel = "llama3.2:1b")
    {
        _endpoint = endpoint.TrimEnd('/');
        _defaultModel = defaultModel;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
    }

    public void UpdateEndpoint(string endpoint)
    {
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            _endpoint = endpoint.Trim().TrimEnd('/');
        }
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await _httpClient.GetAsync($"{_endpoint}/api/tags", cts.Token);
            IsAvailable = response.IsSuccessStatusCode;
            return IsAvailable;
        }
        catch
        {
            IsAvailable = false;
            return false;
        }
    }

    public async Task<List<string>> GetAvailableModelsAsync()
    {
        var models = new List<string>();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            var response = await _httpClient.GetAsync($"{_endpoint}/api/tags", cts.Token);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cts.Token);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("models", out var modelsArray))
                {
                    foreach (var item in modelsArray.EnumerateArray())
                    {
                        if (item.TryGetProperty("name", out var nameProp))
                        {
                            var name = nameProp.GetString();
                            if (!string.IsNullOrEmpty(name))
                            {
                                models.Add(name);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OllamaProvider] Failed to fetch models: {ex.Message}");
        }

        if (models.Count == 0)
        {
            // Default common models
            models.Add(_defaultModel);
            models.Add("llama3.2:1b");
            models.Add("llama3.2:latest");
            models.Add("qwen2.5:1.5b");
            models.Add("mistral:latest");
        }

        return models.Distinct().ToList();
    }

    public async IAsyncEnumerable<string> GenerateStreamAsync(
        string prompt, 
        string? model = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var targetModel = !string.IsNullOrWhiteSpace(model) ? model : _defaultModel;
        var requestPayload = new
        {
            model = targetModel,
            prompt = prompt,
            stream = true
        };

        var json = JsonSerializer.Serialize(requestPayload);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_endpoint}/api/generate")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.TryGetProperty("response", out var respElement))
            {
                var text = respElement.GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    yield return text;
                }
            }

            if (doc.RootElement.TryGetProperty("done", out var doneElement) && doneElement.GetBoolean())
            {
                break;
            }
        }
    }
}
