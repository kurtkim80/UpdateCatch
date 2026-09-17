namespace UpdateCatch.Core.Models;

public class SoftwareTarget
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = "GitHubRelease"; // GitHubRelease, WebChangelog, RssFeed
    public string SourceUrl { get; set; } = string.Empty;
    public string WebChangelogUrl { get; set; } = string.Empty;
    public string RssUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
