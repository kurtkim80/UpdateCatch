using System.Text.RegularExpressions;
using UpdateCatch.Core.Models;

namespace UpdateCatch.Core.Services;

public static class TextChunker
{
    private static readonly Dictionary<string, string[]> TagRules = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Copilot/AI", new[] { "copilot", "ai", "llm", "chat", "artificial intelligence", "reasoning", "gpt", "gemini", "claude" } },
        { "Editor", new[] { "editor", "workbench", "formatting", "indent", "cursor", "minimap", "syntax" } },
        { "Terminal", new[] { "terminal", "powershell", "bash", "zsh", "pty", "shell" } },
        { "Performance", new[] { "performance", "speed", "memory", "cpu", "faster", "optimization", "jit", "latency" } },
        { "Security", new[] { "security", "cve", "vulnerability", "patch", "exploit", "auth", "token" } },
        { "Breaking Change", new[] { "breaking change", "deprecated", "removed", "deprecation", "backward incompatibility" } },
        { "Pricing", new[] { "pricing", "cost", "price", "token price", "free tier", "$/1m" } },
        { "Context-Window", new[] { "context window", "128k", "200k", "1m tokens", "2m tokens", "tokens" } },
        { "PEP", new[] { "pep-", "pep ", "pep" } },
        { ".NET/C#", new[] { "c#", ".net", "asp.net", "blazor", "aot", "clr", "f#", "csharp", "dotnet" } },
        { "macOS", new[] { "macos", "apple", "sequoia", "sonoma", "ios", "xcode", "apple intelligence", "darwin" } },
        { "Windows", new[] { "windows", "win11", "win10", "microsoft windows", "copilot+", "surface", "directx" } },
        { "Linux/Kernel", new[] { "linux", "kernel", "ubuntu", "debian", "fedora", "arch", "distro", "wayland", "gnome", "kde", "systemd", "btrfs", "ext4" } }
    };

    public static List<VectorDocument> ChunkRelease(ReleaseItem release)
    {
        var chunks = new List<VectorDocument>();
        if (string.IsNullOrWhiteSpace(release.Body))
        {
            chunks.Add(new VectorDocument
            {
                Id = $"{release.Id}-0",
                ReleaseItemId = release.Id,
                TargetId = release.TargetId,
                TargetName = release.TargetName,
                Version = release.Version,
                SectionTitle = release.Title,
                Content = release.Title,
                PublishedAt = release.PublishedAt,
                Url = release.HtmlUrl,
                Tags = ExtractTags(release.Title)
            });
            return chunks;
        }

        var lines = release.Body.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var currentSection = release.Title;
        var currentContent = new List<string>();
        int chunkIndex = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("#") && (trimmed.StartsWith("## ") || trimmed.StartsWith("### ") || trimmed.StartsWith("# ")))
            {
                if (currentContent.Count > 0)
                {
                    var contentText = string.Join("\n", currentContent).Trim();
                    if (contentText.Length > 20)
                    {
                        var chunkTags = ExtractTags(currentSection + " " + contentText);
                        chunks.Add(new VectorDocument
                        {
                            Id = $"{release.Id}-{chunkIndex++}",
                            ReleaseItemId = release.Id,
                            TargetId = release.TargetId,
                            TargetName = release.TargetName,
                            Version = release.Version,
                            SectionTitle = currentSection,
                            Content = contentText,
                            PublishedAt = release.PublishedAt,
                            Url = release.HtmlUrl,
                            Tags = chunkTags
                        });
                    }
                    currentContent.Clear();
                }
                currentSection = trimmed.TrimStart('#').Trim();
            }
            else
            {
                currentContent.Add(line);
            }
        }

        if (currentContent.Count > 0)
        {
            var contentText = string.Join("\n", currentContent).Trim();
            if (contentText.Length > 10)
            {
                var chunkTags = ExtractTags(currentSection + " " + contentText);
                chunks.Add(new VectorDocument
                {
                    Id = $"{release.Id}-{chunkIndex++}",
                    ReleaseItemId = release.Id,
                    TargetId = release.TargetId,
                    TargetName = release.TargetName,
                    Version = release.Version,
                    SectionTitle = currentSection,
                    Content = contentText,
                    PublishedAt = release.PublishedAt,
                    Url = release.HtmlUrl,
                    Tags = chunkTags
                });
            }
        }

        if (chunks.Count == 0)
        {
            chunks.Add(new VectorDocument
            {
                Id = $"{release.Id}-0",
                ReleaseItemId = release.Id,
                TargetId = release.TargetId,
                TargetName = release.TargetName,
                Version = release.Version,
                SectionTitle = release.Title,
                Content = release.Body,
                PublishedAt = release.PublishedAt,
                Url = release.HtmlUrl,
                Tags = ExtractTags(release.Body)
            });
        }

        return chunks;
    }

    public static List<string> ExtractTags(string text)
    {
        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in TagRules)
        {
            foreach (var keyword in rule.Value)
            {
                if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    matched.Add(rule.Key);
                    break;
                }
            }
        }
        return matched.ToList();
    }

    public static List<string> ExtractKeyTakeaways(string body, int maxCount = 3)
    {
        var takeaways = new List<string>();
        var lines = body.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var clean = line.Trim();
            if (clean.StartsWith("- ") || clean.StartsWith("* ") || Regex.IsMatch(clean, @"^\d+\.\s"))
            {
                var bulletText = Regex.Replace(clean, @"^[-*]|\d+\.", "").Trim();
                // strip markdown links [text](url) -> text
                bulletText = Regex.Replace(bulletText, @"\[([^\]]+)\]\([^)]+\)", "$1");
                bulletText = bulletText.Replace("**", "").Replace("`", "").Trim();
                if (bulletText.Length >= 15 && bulletText.Length <= 150)
                {
                    takeaways.Add(bulletText);
                    if (takeaways.Count >= maxCount) break;
                }
            }
        }

        if (takeaways.Count == 0 && lines.Length > 0)
        {
            takeaways.Add(lines[0].Trim());
        }

        return takeaways;
    }
}
