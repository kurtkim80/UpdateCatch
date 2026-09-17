using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UpdateCatch.Core.Services;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default);
    int Dimension { get; }
}

public class GeminiEmbeddingService : IEmbeddingService
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    public int Dimension => 768; // text-embedding-004

    public GeminiEmbeddingService(string apiKey, HttpClient? httpClient = null)
    {
        _apiKey = apiKey;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var results = await GenerateEmbeddingsAsync(new List<string> { text }, cancellationToken);
        return results.FirstOrDefault() ?? new float[Dimension];
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default)
    {
        var output = new List<float[]>();
        // Gemini batchEmbedContents API
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/text-embedding-004:batchEmbedContents?key={_apiKey}";
        
        var requests = texts.Select(t => new
        {
            model = "models/text-embedding-004",
            content = new { parts = new[] { new { text = t } } }
        }).ToList();

        var bodyJson = JsonSerializer.Serialize(new { requests });
        using var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var jsonStr = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(jsonStr);
                if (doc.RootElement.TryGetProperty("embeddings", out var embeddingsElem))
                {
                    foreach (var item in embeddingsElem.EnumerateArray())
                    {
                        if (item.TryGetProperty("values", out var vals))
                        {
                            var vec = vals.EnumerateArray().Select(v => v.GetSingle()).ToArray();
                            output.Add(vec);
                        }
                    }
                }
            }
        }
        catch
        {
            // API 오류 시 fallback
        }

        while (output.Count < texts.Count)
        {
            output.Add(new FallbackTfIdfEmbeddingService(Dimension).GenerateEmbedding(texts[output.Count]));
        }

        return output;
    }
}

public class FallbackTfIdfEmbeddingService : IEmbeddingService
{
    private readonly int _dim;
    public int Dimension => _dim;

    public FallbackTfIdfEmbeddingService(int dimension = 128)
    {
        _dim = dimension;
    }

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GenerateEmbedding(text));
    }

    public Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(texts.Select(GenerateEmbedding).ToList());
    }

    public float[] GenerateEmbedding(string text)
    {
        var vec = new float[_dim];
        if (string.IsNullOrWhiteSpace(text)) return vec;

        var tokens = text.ToLowerInvariant().Split(new[] { ' ', '\t', '\n', '\r', ',', '.', '(', ')', '[', ']', '{', '}', '`', '#', ':', '-' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            var hash = MurmurHash(token);
            int idx = Math.Abs(hash % _dim);
            float sign = (hash % 2 == 0) ? 1.0f : -1.0f;
            vec[idx] += sign;
        }

        // L2 Normalize
        float norm = MathF.Sqrt(vec.Sum(x => x * x));
        if (norm > 0.0001f)
        {
            for (int i = 0; i < vec.Length; i++)
            {
                vec[i] /= norm;
            }
        }

        return vec;
    }

    private static int MurmurHash(string str)
    {
        unchecked
        {
            int hash = 23;
            foreach (char c in str)
            {
                hash = hash * 31 + c;
            }
            return hash;
        }
    }
}
