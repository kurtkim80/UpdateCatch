using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UpdateCatch.Core.Models;
using UpdateCatch.Core.Services;

namespace UpdateCatch.Web.Pages;

public class SearchModel : PageModel
{
    private readonly IVectorDatabase _vectorDb;
    private readonly IEmbeddingService _embeddingService;

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Target { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Tag { get; set; }

    public List<SearchResult> Results { get; set; } = new();
    public bool HasSearched { get; set; }

    public SearchModel(IVectorDatabase vectorDb, IEmbeddingService embeddingService)
    {
        _vectorDb = vectorDb;
        _embeddingService = embeddingService;
    }

    public async Task OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(Query))
        {
            HasSearched = false;
            return;
        }

        HasSearched = true;
        var queryVector = await _embeddingService.GenerateEmbeddingAsync(Query);
        Results = await _vectorDb.SearchAsync(queryVector, topK: 12, targetFilter: Target, tagFilter: Tag);
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
        "linux-kernel" => "badge-linux-kernel",
        "ubuntu" => "badge-ubuntu",
        "debian" => "badge-debian",
        "fedora" => "badge-fedora",
        "archlinux" => "badge-archlinux",
        _ => "badge-default"
    };
}
