using System.Net.Http.Headers;
using System.ServiceModel.Syndication;
using System.Text.Json;
using System.Xml;
using HtmlAgilityPack;
using UpdateCatch.Core.Models;

namespace UpdateCatch.Core.Services;

public interface ICrawler
{
    bool CanHandle(SoftwareTarget target);
    Task<List<ReleaseItem>> FetchReleasesAsync(SoftwareTarget target, int maxItems = 5, CancellationToken cancellationToken = default);
}

public class GitHubReleaseCrawler : ICrawler
{
    private readonly HttpClient _httpClient;

    public GitHubReleaseCrawler(HttpClient? httpClient = null, string? githubToken = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "UpdateCatch-Crawler/1.0");
        }
        if (!string.IsNullOrWhiteSpace(githubToken) && !_httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
        }
    }

    public bool CanHandle(SoftwareTarget target) => target.Type.Equals("GitHubRelease", StringComparison.OrdinalIgnoreCase);

    public async Task<List<ReleaseItem>> FetchReleasesAsync(SoftwareTarget target, int maxItems = 5, CancellationToken cancellationToken = default)
    {
        var results = new List<ReleaseItem>();
        try
        {
            var response = await _httpClient.GetAsync(target.SourceUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Warning] Failed to fetch GitHub releases for {target.Name}: {response.StatusCode}");
                return results;
            }

            var jsonStr = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(jsonStr);

            int count = 0;
            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                if (count++ >= maxItems) break;

                var tagName = elem.TryGetProperty("tag_name", out var tagElem) ? tagElem.GetString() ?? "" : "";
                var name = elem.TryGetProperty("name", out var nameElem) ? nameElem.GetString() ?? "" : "";
                var body = elem.TryGetProperty("body", out var bodyElem) ? bodyElem.GetString() ?? "" : "";
                var htmlUrl = elem.TryGetProperty("html_url", out var urlElem) ? urlElem.GetString() ?? "" : "";
                var publishedAt = elem.TryGetProperty("published_at", out var pubElem) && pubElem.TryGetDateTime(out var dt) ? dt : DateTime.UtcNow;

                var version = string.IsNullOrWhiteSpace(tagName) ? name : tagName;
                var title = string.IsNullOrWhiteSpace(name) ? $"{target.Name} {version}" : name;
                var uniqueKey = !string.IsNullOrWhiteSpace(htmlUrl) ? htmlUrl : $"{target.Id}_{version}_{title}";
                var deterministicId = CrawlerHelper.GenerateDeterministicId(target.Id, uniqueKey);

                var releaseItem = new ReleaseItem
                {
                    Id = deterministicId,
                    TargetId = target.Id,
                    TargetName = target.Name,
                    Category = target.Category,
                    Version = version,
                    Title = title,
                    PublishedAt = publishedAt,
                    Body = body,
                    HtmlUrl = htmlUrl,
                    Tags = TextChunker.ExtractTags(title + " " + body),
                    KeyTakeaways = TextChunker.ExtractKeyTakeaways(body)
                };

                results.Add(releaseItem);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] GitHubReleaseCrawler failed for {target.Name}: {ex.Message}");
        }

        return results;
    }
}

public class RssFeedCrawler : ICrawler
{
    private readonly HttpClient _httpClient;

    public RssFeedCrawler(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "UpdateCatch-Crawler/1.0");
        }
    }

    public bool CanHandle(SoftwareTarget target) => !string.IsNullOrWhiteSpace(target.RssUrl);

    public async Task<List<ReleaseItem>> FetchReleasesAsync(SoftwareTarget target, int maxItems = 5, CancellationToken cancellationToken = default)
    {
        var results = new List<ReleaseItem>();
        if (string.IsNullOrWhiteSpace(target.RssUrl)) return results;

        try
        {
            using var stream = await _httpClient.GetStreamAsync(target.RssUrl, cancellationToken);
            using var reader = XmlReader.Create(stream);
            var feed = SyndicationFeed.Load(reader);
            if (feed == null) return results;

            int count = 0;
            foreach (var item in feed.Items)
            {
                if (count++ >= maxItems) break;

                var title = item.Title?.Text ?? $"{target.Name} Update";
                var summary = item.Summary?.Text ?? "";
                var link = item.Links.FirstOrDefault()?.Uri.ToString() ?? target.WebChangelogUrl;
                var pubDate = item.PublishDate != DateTimeOffset.MinValue ? item.PublishDate.UtcDateTime : DateTime.UtcNow;

                // HTML strip if necessary
                var doc = new HtmlDocument();
                doc.LoadHtml(summary);
                var plainText = doc.DocumentNode.InnerText;

                var uniqueKey = !string.IsNullOrWhiteSpace(link) ? link : $"{target.Id}_{title}";
                var deterministicId = CrawlerHelper.GenerateDeterministicId(target.Id, uniqueKey);

                var releaseItem = new ReleaseItem
                {
                    Id = deterministicId,
                    TargetId = target.Id,
                    TargetName = target.Name,
                    Category = target.Category,
                    Version = pubDate.ToString("yyyy-MM"),
                    Title = title,
                    PublishedAt = pubDate,
                    Body = string.IsNullOrWhiteSpace(plainText) ? title : plainText,
                    HtmlUrl = link,
                    Tags = TextChunker.ExtractTags(title + " " + plainText),
                    KeyTakeaways = TextChunker.ExtractKeyTakeaways(plainText)
                };

                results.Add(releaseItem);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] RssFeedCrawler failed for {target.Name}: {ex.Message}");
        }

        return results;
    }
}

public static class CrawlerHelper
{
    public static string GenerateDeterministicId(string targetId, string key)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(key.Trim().ToLowerInvariant());
        var hash = Convert.ToHexString(sha.ComputeHash(bytes))[..12].ToLowerInvariant();
        return $"{targetId}-{hash}";
    }
}

public class WebChangelogCrawler : ICrawler
{
    private readonly HttpClient _httpClient;

    public WebChangelogCrawler(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "UpdateCatch-Crawler/1.0");
        }
    }

    public bool CanHandle(SoftwareTarget target) => target.Type.Equals("WebChangelog", StringComparison.OrdinalIgnoreCase);

    public async Task<List<ReleaseItem>> FetchReleasesAsync(SoftwareTarget target, int maxItems = 5, CancellationToken cancellationToken = default)
    {
        var results = new List<ReleaseItem>();
        try
        {
            var url = !string.IsNullOrWhiteSpace(target.SourceUrl) ? target.SourceUrl : target.WebChangelogUrl;
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Warning] Failed to fetch web changelog for {target.Name}: {response.StatusCode}");
                return results;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Check sections or articles
            var nodes = doc.DocumentNode.SelectNodes("//section | //article | //h2 | //h3");
            if (nodes != null)
            {
                int count = 0;
                foreach (var node in nodes.Take(maxItems))
                {
                    var text = node.InnerText.Trim();
                    if (text.Length < 30) continue;

                    var title = text.Split('\n').FirstOrDefault()?.Trim() ?? $"{target.Name} Update";
                    var deterministicId = CrawlerHelper.GenerateDeterministicId(target.Id, $"{url}_{title}");

                    var releaseItem = new ReleaseItem
                    {
                        Id = deterministicId,
                        TargetId = target.Id,
                        TargetName = target.Name,
                        Category = target.Category,
                        Version = DateTime.UtcNow.ToString("yyyy.MM"),
                        Title = title,
                        PublishedAt = DateTime.UtcNow,
                        Body = text,
                        HtmlUrl = url,
                        Tags = TextChunker.ExtractTags(text),
                        KeyTakeaways = TextChunker.ExtractKeyTakeaways(text)
                    };
                    results.Add(releaseItem);
                }
            }

            // Fallback if no sections detected
            if (results.Count == 0)
            {
                var bodyText = doc.DocumentNode.SelectSingleNode("//main | //body")?.InnerText.Trim() ?? "";
                if (bodyText.Length > 50)
                {
                    var title = $"{target.Name} Latest Updates";
                    var deterministicId = CrawlerHelper.GenerateDeterministicId(target.Id, $"{url}_{title}");

                    results.Add(new ReleaseItem
                    {
                        Id = deterministicId,
                        TargetId = target.Id,
                        TargetName = target.Name,
                        Category = target.Category,
                        Version = DateTime.UtcNow.ToString("yyyy.MM"),
                        Title = title,
                        PublishedAt = DateTime.UtcNow,
                        Body = bodyText.Length > 2000 ? bodyText[..2000] : bodyText,
                        HtmlUrl = url,
                        Tags = TextChunker.ExtractTags(bodyText),
                        KeyTakeaways = TextChunker.ExtractKeyTakeaways(bodyText)
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] WebChangelogCrawler failed for {target.Name}: {ex.Message}");
        }

        return results;
    }
}
