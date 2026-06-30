using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cohere.LoanProcessing.WebApp.Configuration;
using Cohere.LoanProcessing.WebApp.Models;
using Microsoft.Extensions.Options;

namespace Cohere.LoanProcessing.WebApp.Services;

public sealed class DatasetSeedCatalogService
{
    private static readonly JsonSerializerOptions CatalogJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _datasetRoot;
    private readonly Lazy<IReadOnlyList<SeedCaseDefinition>> _cases;

    public DatasetSeedCatalogService(IWebHostEnvironment environment, IOptions<DatasetSeedOptions> options)
    {
        _datasetRoot = ResolveDatasetRoot(environment.ContentRootPath, options.Value.RootPath);
        _cases = new Lazy<IReadOnlyList<SeedCaseDefinition>>(LoadCases, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IReadOnlyList<SeedCaseDefinition> GetAllCases() => _cases.Value;

    public SeedCaseDefinition? TryGetCase(string caseId) =>
        _cases.Value.FirstOrDefault(seedCase =>
            string.Equals(seedCase.CaseId, caseId.Trim(), StringComparison.OrdinalIgnoreCase));

    public string DatasetRoot => _datasetRoot;

    private IReadOnlyList<SeedCaseDefinition> LoadCases()
    {
        string catalogPath = Path.Combine(_datasetRoot, "cases", "catalog.json");
        if (!File.Exists(catalogPath))
        {
            throw new FileNotFoundException($"Case catalog not found at '{catalogPath}'.");
        }

        CaseCatalogEntry[]? entries = JsonSerializer.Deserialize<CaseCatalogEntry[]>(
            File.ReadAllText(catalogPath),
            CatalogJsonOptions);

        if (entries is null || entries.Length == 0)
        {
            throw new InvalidOperationException("Case catalog is empty.");
        }

        return entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.CaseId))
            .OrderBy(entry => entry.CaseId, StringComparer.OrdinalIgnoreCase)
            .Select(BuildCaseDefinition)
            .ToList();
    }

    private SeedCaseDefinition BuildCaseDefinition(CaseCatalogEntry entry)
    {
        string caseId = entry.CaseId.Trim();
        string applicationPath = Path.Combine(_datasetRoot, "cases", caseId, "ingest", "loan_application.txt");
        if (!File.Exists(applicationPath))
        {
            throw new FileNotFoundException($"Missing loan application seed file for case '{caseId}'.", applicationPath);
        }

        ParsedLoanApplication parsed = LoanApplicationTextParser.Parse(File.ReadAllText(applicationPath), caseId);
        string borrower = entry.Context?.Borrower ?? parsed.BorrowerName;
        string expectedOutcome = MapExpectedOutcome(entry.Context?.ExpectedDecision);
        string description = string.IsNullOrWhiteSpace(entry.Description)
            ? $"Mortgage application for {borrower}."
            : entry.Description.Trim();

        return new SeedCaseDefinition(
            CaseId: caseId,
            BorrowerName: borrower,
            CoBorrowerName: entry.Context?.CoBorrower ?? parsed.CoBorrowerName,
            RequestedLoanAmount: parsed.RequestedLoanAmount > 0 ? parsed.RequestedLoanAmount : 0m,
            PurchasePrice: parsed.PurchasePrice,
            MonthlyIncome: parsed.MonthlyIncome,
            MonthlyDebt: parsed.MonthlyDebt,
            PropertyAddress: parsed.PropertyAddress,
            PropertyCityState: parsed.PropertyCityState,
            PropertyType: parsed.PropertyType,
            Product: parsed.Product,
            Purpose: parsed.Purpose,
            ExpectedOutcome: expectedOutcome,
            PrimaryReason: entry.Context?.PrimaryReason,
            OccupationTitle: null,
            Description: description,
            DemoTagline: BuildDemoTagline(entry, parsed));
    }

    private static string BuildDemoTagline(CaseCatalogEntry entry, ParsedLoanApplication parsed)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(parsed.Product))
        {
            parts.Add(parsed.Product);
        }

        if (!string.IsNullOrWhiteSpace(entry.OutcomeTag))
        {
            parts.Add(entry.OutcomeTag);
        }

        return parts.Count == 0 ? "Dataset seed application" : string.Join(" · ", parts);
    }

    private static string MapExpectedOutcome(string? decision) =>
        decision?.Trim().ToLowerInvariant() switch
        {
            "approve" => "approve",
            "deny" => "deny",
            "manual_review" => "review",
            _ => "review"
        };

    internal static string ResolveDatasetRoot(string contentRootPath, string? configuredRoot)
    {
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            string resolved = Path.GetFullPath(
                Path.IsPathRooted(configuredRoot)
                    ? configuredRoot
                    : Path.Combine(contentRootPath, configuredRoot));

            if (Directory.Exists(resolved))
            {
                return resolved;
            }
        }

        var current = new DirectoryInfo(contentRootPath);
        while (current is not null)
        {
            string candidate = Path.Combine(current.FullName, "dataset-seed");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(Path.Combine(contentRootPath, "..", "..", "..", "dataset-seed"));
    }

    private sealed class CaseCatalogEntry
    {
        [JsonPropertyName("caseId")]
        public string CaseId { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("outcomeTag")]
        public string? OutcomeTag { get; set; }

        [JsonPropertyName("context")]
        public CaseCatalogContext? Context { get; set; }
    }

    private sealed class CaseCatalogContext
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
}
