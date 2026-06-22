using Cohere.LoanProcessing.WebApp.Components;
using Cohere.LoanProcessing.WebApp.Configuration;
using Cohere.LoanProcessing.WebApp.Services;
using Cohere.LoanProcessing.WebApp.State;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://cohereloan-api-7d4ofvyd6i5mg.livelyriver-65dc46d9.westus.azurecontainerapps.io/";
builder.Services.AddHttpClient<LoanApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl));
builder.Services.Configure<WorkflowPollingOptions>(builder.Configuration.GetSection("WorkflowPolling"));
builder.Services.Configure<DatasetSeedOptions>(builder.Configuration.GetSection("DatasetSeed"));
builder.Services.AddSingleton<DatasetSeedCatalogService>();
builder.Services.AddScoped<CaseSessionStore>();
builder.Services.AddScoped<CaseWorkspaceState>();
builder.Services.AddScoped<CaseWorkspaceSectionState>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
