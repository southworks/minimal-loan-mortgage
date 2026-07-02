using CohereLoanAndMortgage.Api.Host.Options;
using CohereLoanAndMortgage.Api.Host.Services;
using CohereLoanAndMortgage.Api.Host.Workflow;
using LoanWorkflow.Mcp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAzureMonitorTelemetryIfConfigured(builder.Configuration);
builder.Services.AddControllers();

builder.Services.Configure<AzureFoundryOptions>(options =>
{
    builder.Configuration.GetSection(AzureFoundryOptions.SectionName).Bind(options);

    string? endpoint = builder.Configuration["AZURE_FOUNDRY_PROJECT_ENDPOINT"]
        ?? Environment.GetEnvironmentVariable("AZURE_FOUNDRY_PROJECT_ENDPOINT");

    if (!string.IsNullOrWhiteSpace(endpoint))
    {
        options.ProjectEndpoint = endpoint;
    }
});

builder.Services.Configure<DocumentExtractionOptions>(
    builder.Configuration.GetSection(DocumentExtractionOptions.SectionName));
builder.Services.Configure<CaseWorkflowOptions>(
    builder.Configuration.GetSection(CaseWorkflowOptions.SectionName));

StartupConfigurationValidator.Validate(builder.Configuration);

builder.Services.AddSingleton<FoundryAgentProvider>();
builder.Services.AddSingleton<LoanMortgageBasicWorkflowFactory>();
builder.Services.AddSingleton<LocalCaseDocumentService>();
builder.Services.AddSingleton<DocumentTextExtractionService>();
builder.Services.AddLoanWorkflowMcpServices(builder.Configuration);
builder.Services.AddSingleton<CaseEvidenceIndexingService>();
builder.Services.AddSingleton<InMemoryBasicWorkflowStore>();
builder.Services.AddSingleton<BasicLoanWorkflowService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();
