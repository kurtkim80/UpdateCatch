using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UpdateCatch.Core.Models;

namespace UpdateCatch.Web.Pages;

public class TargetsModel : PageModel
{
    private readonly IWebHostEnvironment _env;
    public List<SoftwareTarget> Targets { get; set; } = new();

    public TargetsModel(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task OnGetAsync()
    {
        var repoRoot = FindRepoRoot(_env.ContentRootPath);
        var path = Path.Combine(repoRoot, "targets.json");
        if (System.IO.File.Exists(path))
        {
            var json = await System.IO.File.ReadAllTextAsync(path);
            Targets = JsonSerializer.Deserialize<List<SoftwareTarget>>(json) ?? new List<SoftwareTarget>();
        }
    }

    private static string FindRepoRoot(string currentDir)
    {
        var dir = new DirectoryInfo(currentDir);
        while (dir != null)
        {
            if (System.IO.File.Exists(Path.Combine(dir.FullName, "targets.json")) ||
                System.IO.File.Exists(Path.Combine(dir.FullName, "UpdateCatch.sln")) ||
                Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return currentDir;
    }
}
