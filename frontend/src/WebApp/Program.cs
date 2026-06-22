using Cohere.LoanProcessing.WebApp.Components;
using Cohere.LoanProcessing.WebApp.Configuration;
using Cohere.LoanProcessing.WebApp.Services;
using Cohere.LoanProcessing.WebApp.State;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5038/";
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

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
