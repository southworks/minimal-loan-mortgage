using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CohereLoanAndMortgage.Api.Host.Contracts;
using LoanWorkflow.Mcp.Options;
using Microsoft.Extensions.Options;

namespace CohereLoanAndMortgage.Api.Host.Services;

public sealed class ScenarioCatalogService
{
    private static readonly JsonSerializerOptions MatrixJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _datasetRoot;
    private readonly Lazy<IReadOnlyList<ScenarioSummaryResponse>> _scenarios;

    public ScenarioCatalogService(IHostEnvironment environment, IOptions<DatasetOptions> options)
    {
        _datasetRoot = LocalCaseDocumentService.ResolveDatasetRoot(environment.ContentRootPath, options.Value.RootPath);
        _scenarios = new Lazy<IReadOnlyList<ScenarioSummaryResponse>>(
            LoadScenarios,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public Task<IReadOnlyList<ScenarioSummaryResponse>> GetScenariosAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_scenarios.Value);
    }

    private IReadOnlyList<ScenarioSummaryResponse> LoadScenarios()
    {
        var casesRoot = Path.Combine(_datasetRoot, "cases");
        if (!Directory.Exists(casesRoot))
        {
            throw new DirectoryNotFoundException(
                $"Dataset seed folder was not found at '{casesRoot}'. Configure Dataset:RootPath if needed.");
        }

        var matrixById = LoadCaseMatrix();

        return Directory
            .EnumerateDirectories(casesRoot)
            .Select(directory => Path.GetFileName(directory))
            .Where(caseId => !string.IsNullOrWhiteSpace(caseId))
            .OrderBy(caseId => caseId, StringComparer.OrdinalIgnoreCase)
            .Select(caseId => BuildScenario(caseId!, casesRoot, matrixById))
            .ToList();
    }

    private ScenarioSummaryResponse BuildScenario(
        string caseId,
        string casesRoot,
        IReadOnlyDictionary<string, CaseMatrixEntry> matrixById)
    {
        string applicationPath = Path.Combine(casesRoot, caseId, "ingest", "loan_application.txt");
        if (!File.Exists(applicationPath))
        {
            throw new FileNotFoundException($"Missing loan application seed file for case '{caseId}'.", applicationPath);
        }

        ParsedLoanApplication parsed = LoanApplicationTextParser.Parse(File.ReadAllText(applicationPath), caseId);
        matrixById.TryGetValue(caseId, out CaseMatrixEntry? matrixEntry);

        string expectedOutcome = MapExpectedOutcome(matrixEntry?.Decision);
        string borrower = matrixEntry?.Borrower ?? parsed.BorrowerName;
        decimal requestedLoanAmount = parsed.RequestedLoanAmount > 0
            ? parsed.RequestedLoanAmount
            : matrixEntry?.RequestedLoan ?? 0m;
        string propertySummary = BuildPropertySummary(parsed, matrixEntry);
        string description =
            $"Mortgage application {caseId} for {borrower}. {propertySummary} Requested loan {FormatCurrency(requestedLoanAmount)}.";

        return new ScenarioSummaryResponse
        {
            CaseId = caseId,
            Title = FormatLoanTitle(borrower, requestedLoanAmount),
            Description = description.Trim(),
            ExpectedOutcome = expectedOutcome,
            DemoTagline = BuildDemoTagline(parsed, matrixEntry)
        };
    }

    private static string FormatLoanTitle(string borrowerName, decimal requestedLoanAmount) =>
        $"{borrowerName} loan for {decimal.Truncate(requestedLoanAmount).ToString("0", CultureInfo.InvariantCulture)} USD";

    private static string BuildPropertySummary(ParsedLoanApplication parsed, CaseMatrixEntry? matrixEntry)
    {
        string? address = matrixEntry?.PropertyAddress ?? parsed.PropertyAddress;
        if (!string.IsNullOrWhiteSpace(address))
        {
            return $"Property at {address}.";
        }

        if (!string.IsNullOrWhiteSpace(parsed.PropertyCityState))
        {
            return $"Property in {parsed.PropertyCityState}.";
        }

        return "Synthetic mortgage application package.";
    }

    private static string BuildDemoTagline(ParsedLoanApplication parsed, CaseMatrixEntry? matrixEntry)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(parsed.Product))
        {
            parts.Add(parsed.Product);
        }

        if (!string.IsNullOrWhiteSpace(matrixEntry?.Title))
        {
            parts.Add(matrixEntry.Title);
        }

        if (matrixEntry?.Score is int score)
        {
            parts.Add($"Credit score {score}");
        }

        return parts.Count == 0 ? "Dataset seed application" : string.Join(" · ", parts);
    }

    private Dictionary<string, CaseMatrixEntry> LoadCaseMatrix()
    {
        string catalogPath = Path.Combine(_datasetRoot, "cases", "catalog.json");
        if (!File.Exists(catalogPath))
        {
            return new Dictionary<string, CaseMatrixEntry>(StringComparer.OrdinalIgnoreCase);
        }

        CatalogEntry[]? entries = JsonSerializer.Deserialize<CatalogEntry[]>(File.ReadAllText(catalogPath), MatrixJsonOptions);
        return entries?
                   .Where(entry => !string.IsNullOrWhiteSpace(entry.CaseId))
                   .ToDictionary(
                       entry => entry.CaseId,
                       entry => new CaseMatrixEntry
                       {
                           Id = entry.CaseId,
                           Borrower = entry.Context?.Borrower,
                           CoBorrower = entry.Context?.CoBorrower,
                           Decision = entry.Context?.ExpectedDecision,
                           PrimaryReason = entry.Context?.PrimaryReason
                       },
                       StringComparer.OrdinalIgnoreCase)
               ?? new Dictionary<string, CaseMatrixEntry>(StringComparer.OrdinalIgnoreCase);
    }

    private static string MapExpectedOutcome(string? decision) =>
        decision?.Trim().ToLowerInvariant() switch
        {
            "approve" => "approve",
            "deny" => "deny",
            "manual_review" => "review",
            _ => "review"
        };

    private static string FormatCurrency(decimal amount) => amount.ToString("C0", CultureInfo.CurrentCulture);

    private sealed class CatalogEntry
    {
        [JsonPropertyName("caseId")]
        public string CaseId { get; set; } = string.Empty;

        [JsonPropertyName("context")]
        public CatalogContext? Context { get; set; }
    }

    private sealed class CatalogContext
    {
        [JsonPropertyName("borrower")]
        public string? Borrower { get; set; }

        [JsonPropertyName("coBorrower")]
        public string? CoBorrower { get; set; }

        [JsonPropertyName("expectedDecision")]
        public string? ExpectedDecision { get; set; }

        [JsonPropertyName("primaryReason")]
        public string? PrimaryReason { get; set; }
    }

    private sealed class CaseMatrixEntry
    {
        public string Id { get; set; } = string.Empty;

        public string? Borrower { get; set; }

        public string? CoBorrower { get; set; }

        public decimal? RequestedLoan { get; set; }

        public decimal? PurchasePrice { get; set; }

        public decimal? MonthlyIncome { get; set; }

        public decimal? MonthlyDebt { get; set; }

        public string? PropertyAddress { get; set; }

        public string? PropertyType { get; set; }

        public string? Title { get; set; }

        public string? Decision { get; set; }

        public string? PrimaryReason { get; set; }

        public int? Score { get; set; }
    }
}
