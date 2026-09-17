namespace UpdateCatch.Core.Models;

public class ReleaseItem
{
    public string Id { get; set; } = string.Empty; // e.g. "vscode-1.95.0"
    public string TargetId { get; set; } = string.Empty; // "vscode", "python", "gemini", etc.
    public string TargetName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public string Body { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public List<string> KeyTakeaways { get; set; } = new();
}

public class VectorDocument
{
    public string Id { get; set; } = string.Empty; // unique chunk id
    public string ReleaseItemId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string SectionTitle { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public string Url { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public float[] Vector { get; set; } = Array.Empty<float>();
}

public class SearchResult
{
    public VectorDocument Document { get; set; } = new();
    public float Score { get; set; }
}
