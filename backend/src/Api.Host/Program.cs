using CohereLoanAndMortgage.Api.Host.Options;
using CohereLoanAndMortgage.Api.Host.Services;
using CohereLoanAndMortgage.Api.Host.Workflow;
using LoanWorkflow.Mcp;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.Configure<AzureBlobStorageOptions>(options =>
{
    builder.Configuration.GetSection(AzureBlobStorageOptions.SectionName).Bind(options);

    string? connectionString = builder.Configuration["AZURE_STORAGE_CONNECTION_STRING"]
        ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        options.ConnectionString = connectionString;
    }

    string? blobServiceUri = builder.Configuration["AZURE_STORAGE_BLOB_SERVICE_URI"]
        ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_BLOB_SERVICE_URI");

    if (!string.IsNullOrWhiteSpace(blobServiceUri))
    {
        options.BlobServiceUri = blobServiceUri;
    }
});

builder.Services.Configure<DocumentExtractionOptions>(
    builder.Configuration.GetSection(DocumentExtractionOptions.SectionName));
builder.Services.Configure<CaseWorkflowOptions>(
    builder.Configuration.GetSection(CaseWorkflowOptions.SectionName));

StartupConfigurationValidator.Validate(builder.Configuration);

builder.Services.AddSingleton<FoundryAgentProvider>();
builder.Services.AddSingleton<LoanMortgageBasicWorkflowFactory>();
builder.Services.AddSingleton<BlobDocumentStorageService>();
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
