using System.Text.Json;
using UpdateCatch.Core.Models;
using UpdateCatch.Core.Services;

Console.WriteLine("==================================================");
Console.WriteLine("        UpdateCatch - Release Collector          ");
Console.WriteLine("==================================================");

var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var targetsPath = Path.Combine(repoRoot, "targets.json");
var dataDir = Path.Combine(repoRoot, "data");

Console.WriteLine($"[Config] Repo root: {repoRoot}");
Console.WriteLine($"[Config] Targets file: {targetsPath}");
Console.WriteLine($"[Config] Storage dir: {dataDir}");

if (!File.Exists(targetsPath))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[Error] targets.json not found at {targetsPath}");
    Console.ResetColor();
    return 1;
}

// 1. Load targets
var targetsJson = await File.ReadAllTextAsync(targetsPath);
var targets = JsonSerializer.Deserialize<List<SoftwareTarget>>(targetsJson) ?? new List<SoftwareTarget>();
Console.WriteLine($"[Info] Loaded {targets.Count} software target(s).");

// 2. Setup services
var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
var geminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

using var httpClient = new HttpClient();
var crawlers = new List<ICrawler>
{
    new GitHubReleaseCrawler(httpClient, githubToken),
    new RssFeedCrawler(httpClient),
    new WebChangelogCrawler(httpClient)
};

IEmbeddingService embeddingService = !string.IsNullOrWhiteSpace(geminiApiKey)
    ? new GeminiEmbeddingService(geminiApiKey, httpClient)
    : new FallbackTfIdfEmbeddingService(128);

Console.WriteLine($"[Info] Using Embedding Provider: {embeddingService.GetType().Name} (Dimension: {embeddingService.Dimension})");

var vectorDb = new JsonFileVectorDatabase(dataDir);

// 3. Process each target
var allReleases = new List<ReleaseItem>();
var allDocuments = new List<VectorDocument>();

foreach (var target in targets)
{
    Console.WriteLine($"\n--> Processing [{target.Category}] {target.Name} (Type: {target.Type})...");
    
    // Pick crawler
    var crawler = crawlers.FirstOrDefault(c => c.CanHandle(target));
    if (crawler == null)
    {
        Console.WriteLine($"    [Warning] No suitable crawler for target {target.Name}");
        continue;
    }

    var releases = await crawler.FetchReleasesAsync(target, maxItems: 3);
    Console.WriteLine($"    Found {releases.Count} release(s).");

    foreach (var rel in releases)
    {
        allReleases.Add(rel);
        var chunks = TextChunker.ChunkRelease(rel);
        Console.WriteLine($"    - Version: {rel.Version} | Chunks: {chunks.Count} | Tags: [{string.Join(", ", rel.Tags)}]");

        // Generate embeddings for chunks
        var textsToEmbed = chunks.Select(c => $"{c.TargetName} {c.Version} {c.SectionTitle}\n{c.Content}").ToList();
        var embeddings = await embeddingService.GenerateEmbeddingsAsync(textsToEmbed);
        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].Vector = embeddings[i];
            allDocuments.Add(chunks[i]);
        }
    }
}

// 4. Upsert to Vector DB
Console.WriteLine($"\n[Storage] Saving {allReleases.Count} releases and {allDocuments.Count} vector chunks into Vector DB...");
await vectorDb.UpsertReleasesAsync(allReleases);
await vectorDb.UpsertDocumentsAsync(allDocuments);

// Sync to docs/data for GitHub Pages live hosting
var docsDataDir = Path.Combine(repoRoot, "docs", "data");
if (Directory.Exists(Path.Combine(repoRoot, "docs")))
{
    Directory.CreateDirectory(docsDataDir);
    foreach (var file in Directory.GetFiles(dataDir))
    {
        var dest = Path.Combine(docsDataDir, Path.GetFileName(file));
        File.Copy(file, dest, overwrite: true);
    }
    Console.WriteLine($"[Storage] Synced data to {docsDataDir} for GitHub Pages!");
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("\n[Success] UpdateCatch collection and vector indexing completed successfully!");
Console.ResetColor();
return 0;

static string FindRepoRoot(string currentDir)
{
    var dir = new DirectoryInfo(currentDir);
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "targets.json")) ||
            File.Exists(Path.Combine(dir.FullName, "UpdateCatch.sln")) ||
            Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            return dir.FullName;
        }
        dir = dir.Parent;
    }
    return currentDir;
}
