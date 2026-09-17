using Microsoft.AspNetCore.Mvc.RazorPages;
using UpdateCatch.Core.Models;
using UpdateCatch.Core.Services;

namespace UpdateCatch.Web.Pages;

public class IndexModel : PageModel
{
    private readonly IVectorDatabase _vectorDb;

    public List<ReleaseItem> Releases { get; set; } = new();
    public string? SelectedTarget { get; set; }

    public IndexModel(IVectorDatabase vectorDb)
    {
        _vectorDb = vectorDb;
    }

    public async Task OnGetAsync(string? target = null)
    {
        SelectedTarget = target;
        Releases = await _vectorDb.GetRecentReleasesAsync(count: 30, targetFilter: SelectedTarget);
    }

    public string GetBadgeClass(string targetId) => targetId.ToLowerInvariant() switch
    {
        "vscode" => "badge-vscode",
        "python" => "badge-python",
        "gemini" => "badge-gemini",
        "claude" => "badge-claude",
        "openai" => "badge-openai",
        "dotnet" => "badge-dotnet",
        "macos" => "badge-macos",
        "windows" => "badge-windows",
        _ => "badge-default"
    };

    public string GetTagClass(string tag) => tag.ToLowerInvariant() switch
    {
        "copilot/ai" => "tag-ai",
        "security" => "tag-security",
        "breaking change" => "tag-breaking",
        "performance" => "tag-perf",
        _ => ""
    };
}
