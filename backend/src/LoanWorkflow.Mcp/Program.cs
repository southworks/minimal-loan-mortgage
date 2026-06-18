using System.Collections.Concurrent;
using LoanWorkflow.Mcp;
using LoanWorkflow.Mcp.Startup;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLoanWorkflowMcpServices(builder.Configuration);
builder.Services.AddHostedService<McpStartupInitializer>();

var toolDictionary = new ConcurrentDictionary<string, McpServerTool[]>(StringComparer.OrdinalIgnoreCase);

builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = true;
        options.ConfigureSessionOptions = (httpContext, mcpOptions, _) =>
        {
            var path = httpContext.Request.Path.Value ?? string.Empty;
            var serverKey = ResolveServerKey(path);

            if (!toolDictionary.TryGetValue(serverKey, out var tools))
            {
                return Task.CompletedTask;
            }

            mcpOptions.ToolCollection = [];
            foreach (var tool in tools)
            {
                mcpOptions.ToolCollection.Add(tool);
            }

            return Task.CompletedTask;
        };
    });

var app = builder.Build();

ServiceCollectionExtensions.PopulateToolDictionary(app.Services, toolDictionary);

app.MapMcp("/document-retrieval/mcp");
app.MapMcp("/underwriting-rules/mcp");
app.MapMcp("/policy-knowledge/mcp");
app.MapMcp("/loan-setup/mcp");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

static string ResolveServerKey(string path)
{
    if (path.Contains("/document-retrieval/", StringComparison.OrdinalIgnoreCase))
    {
        return "document-retrieval";
    }

    if (path.Contains("/underwriting-rules/", StringComparison.OrdinalIgnoreCase))
    {
        return "underwriting-rules";
    }

    if (path.Contains("/policy-knowledge/", StringComparison.OrdinalIgnoreCase))
    {
        return "policy-knowledge";
    }

    if (path.Contains("/loan-setup/", StringComparison.OrdinalIgnoreCase))
    {
        return "loan-setup";
    }

    return "document-retrieval";
}
