[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ProxyTests")]

// Cargar .env automáticamente si existe — collect vars for UI logging
var envVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
string envPath = Path.Combine(AppContext.BaseDirectory, ".env");
if (!File.Exists(envPath))
{
    envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
}
if (File.Exists(envPath))
{
    foreach (string line in File.ReadAllLines(envPath))
    {
        string trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith("#"))
            continue;
        int eq = trimmed.IndexOf('=');
        if (eq < 1)
            continue;
        string key = trimmed[..eq].Trim();
        string value = trimmed[(eq + 1)..].Trim().Trim('"');
        if (!string.IsNullOrEmpty(key))
        {
            Environment.SetEnvironmentVariable(key, value);
            envVars[key] = value;
        }
    }
}

WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);

int port = int.TryParse(Environment.GetEnvironmentVariable("PROXY_PORT"), out int p) ? p : 11434;
string? proxyApiKey = Environment.GetEnvironmentVariable("PROXY_API_KEY");

builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddSingleton<ProviderHttpClientFactory>();
builder.Services.AddSingleton<ProviderRegistry>();
builder.Services.AddSingleton<ModelSelectionStore>();
builder.Services.AddSingleton<ModelCatalogService>();
builder.Services.AddSingleton<ReasoningCacheService>();
builder.Services.AddSingleton<RequestTransformer>();
builder.Services.AddSingleton<OllamaResponseBuilder>();
builder.Services.AddSingleton<ChatStreamingService>();

builder.Services.AddHostedService<ProviderBenchmarkService>();
builder.Services.AddSingleton<AiProxyHub.TextWriterLoggerProvider>();
builder.Services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<AiProxyHub.TextWriterLoggerProvider>());

WebApplication app = builder.Build();
app.UseOptionalProxyAuthentication(proxyApiKey);

ModelCatalogService modelCatalog = app.Services.GetRequiredService<ModelCatalogService>();
await modelCatalog.RefreshAvailableModels(CancellationToken.None);

app.MapOpenAiEndpoints();
app.MapOllamaEndpoints();
app.MapHealthEndpoints();

// Start the web server (non-blocking)
await app.StartAsync();

// ── Windows Forms UI ────────────────────────────────────────────
Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

using var mainForm = new AiProxyHub.MainForm(port, app.Services, envVars);

// Redirect all Console output to the form's log box
var formWriter = new AiProxyHub.FormLogWriter(mainForm);
app.Services.GetRequiredService<AiProxyHub.TextWriterLoggerProvider>().SetWriter(formWriter);
Console.SetOut(formWriter);
Console.SetError(formWriter);

Application.Run(mainForm);

await app.StopAsync();
await app.DisposeAsync();
public partial class Program { }
