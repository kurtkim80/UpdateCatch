using System.Text.Json;
using UpdateCatch.Core.Models;

namespace UpdateCatch.Core.Services;

public interface IVectorDatabase
{
    Task UpsertDocumentsAsync(IEnumerable<VectorDocument> documents, CancellationToken cancellationToken = default);
    Task UpsertReleasesAsync(IEnumerable<ReleaseItem> releases, CancellationToken cancellationToken = default);
    Task<List<SearchResult>> SearchAsync(float[] queryVector, int topK = 10, string? targetFilter = null, string? tagFilter = null, CancellationToken cancellationToken = default);
    Task<List<ReleaseItem>> GetRecentReleasesAsync(int count = 20, string? targetFilter = null, CancellationToken cancellationToken = default);
    Task<ReleaseItem?> GetReleaseByIdAsync(string id, CancellationToken cancellationToken = default);
}

public class JsonFileVectorDatabase : IVectorDatabase
{
    private readonly string _storageDir;
    private readonly string _docsFilePath;
    private readonly string _releasesFilePath;
    private static readonly SemaphoreSlim _fileLock = new(1, 1);

    public JsonFileVectorDatabase(string? storageDir = null)
    {
        _storageDir = storageDir ?? Path.Combine(Directory.GetCurrentDirectory(), "data");
        Directory.CreateDirectory(_storageDir);
        _docsFilePath = Path.Combine(_storageDir, "vectors.json");
        _releasesFilePath = Path.Combine(_storageDir, "releases.json");
    }

    public async Task UpsertDocumentsAsync(IEnumerable<VectorDocument> documents, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var existing = await LoadDocumentsInternalAsync(cancellationToken);
            var map = existing.ToDictionary(d => d.Id, d => d);
            foreach (var doc in documents)
            {
                map[doc.Id] = doc;
            }

            var json = JsonSerializer.Serialize(map.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_docsFilePath, json, cancellationToken);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task UpsertReleasesAsync(IEnumerable<ReleaseItem> releases, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var existing = await LoadReleasesInternalAsync(cancellationToken);
            
            // Deduplicate by Id, (TargetId, Title), and (TargetId, HtmlUrl)
            var releaseList = new List<ReleaseItem>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 신규 데이터 우선 (최신 정보로 갱신)
            foreach (var rel in releases.Concat(existing))
            {
                var titleKey = $"{rel.TargetId}__title__{rel.Title.Trim()}";
                var urlKey = !string.IsNullOrWhiteSpace(rel.HtmlUrl) ? $"{rel.TargetId}__url__{rel.HtmlUrl.Trim()}" : titleKey;

                if (seenKeys.Add(rel.Id) && seenKeys.Add(titleKey) && seenKeys.Add(urlKey))
                {
                    releaseList.Add(rel);
                }
            }

            var json = JsonSerializer.Serialize(releaseList.OrderByDescending(r => r.PublishedAt).ToList(), new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_releasesFilePath, json, cancellationToken);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<List<SearchResult>> SearchAsync(float[] queryVector, int topK = 10, string? targetFilter = null, string? tagFilter = null, CancellationToken cancellationToken = default)
    {
        var docs = await LoadDocumentsInternalAsync(cancellationToken);
        var filtered = docs.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(targetFilter))
        {
            filtered = filtered.Where(d => string.Equals(d.TargetId, targetFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(tagFilter))
        {
            filtered = filtered.Where(d => d.Tags.Any(t => string.Equals(t, tagFilter, StringComparison.OrdinalIgnoreCase)));
        }

        var results = new List<SearchResult>();
        foreach (var doc in filtered)
        {
            if (doc.Vector == null || doc.Vector.Length == 0) continue;
            float sim = CosineSimilarity(queryVector, doc.Vector);
            results.Add(new SearchResult
            {
                Document = doc,
                Score = sim
            });
        }

        return results.OrderByDescending(r => r.Score).Take(topK).ToList();
    }

    public async Task<List<ReleaseItem>> GetRecentReleasesAsync(int count = 20, string? targetFilter = null, CancellationToken cancellationToken = default)
    {
        var releases = await LoadReleasesInternalAsync(cancellationToken);
        var query = releases.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(targetFilter))
        {
            query = query.Where(r => string.Equals(r.TargetId, targetFilter, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderByDescending(r => r.PublishedAt).Take(count).ToList();
    }

    public async Task<ReleaseItem?> GetReleaseByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var releases = await LoadReleasesInternalAsync(cancellationToken);
        return releases.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<VectorDocument>> LoadDocumentsInternalAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_docsFilePath)) return new List<VectorDocument>();
        try
        {
            var json = await File.ReadAllTextAsync(_docsFilePath, cancellationToken);
            return JsonSerializer.Deserialize<List<VectorDocument>>(json) ?? new List<VectorDocument>();
        }
        catch
        {
            return new List<VectorDocument>();
        }
    }

    private async Task<List<ReleaseItem>> LoadReleasesInternalAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_releasesFilePath)) return new List<ReleaseItem>();
        try
        {
            var json = await File.ReadAllTextAsync(_releasesFilePath, cancellationToken);
            return JsonSerializer.Deserialize<List<ReleaseItem>>(json) ?? new List<ReleaseItem>();
        }
        catch
        {
            return new List<ReleaseItem>();
        }
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        int len = Math.Min(a.Length, b.Length);
        if (len == 0) return 0f;

        float dot = 0f, magA = 0f, magB = 0f;
        for (int i = 0; i < len; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA <= 0.000001f || magB <= 0.000001f) return 0f;
        return dot / (MathF.Sqrt(magA) * MathF.Sqrt(magB));
    }
}
