using UpdateCatch.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Setup Storage and Embedding DI
var contentRoot = builder.Environment.ContentRootPath;
var repoRoot = FindRepoRoot(contentRoot);
var dataDir = Path.Combine(repoRoot, "data");

builder.Services.AddSingleton<IVectorDatabase>(sp => new JsonFileVectorDatabase(dataDir));

var geminiKey = builder.Configuration["GEMINI_API_KEY"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
builder.Services.AddSingleton<IEmbeddingService>(sp =>
{
    if (!string.IsNullOrWhiteSpace(geminiKey))
    {
        return new GeminiEmbeddingService(geminiKey);
    }
    return new FallbackTfIdfEmbeddingService(128);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();

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
