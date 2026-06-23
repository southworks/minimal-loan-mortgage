using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cohere.LoanProcessing.WebApp.Configuration;
using Cohere.LoanProcessing.WebApp.Models;
using Microsoft.Extensions.Options;

namespace Cohere.LoanProcessing.WebApp.Services;

public sealed class DatasetSeedCatalogService
{
    private static readonly JsonSerializerOptions MatrixJsonOptions = new()
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
            string.Equals(seedCase.CaseId, caseId, StringComparison.OrdinalIgnoreCase));

    public string DatasetRoot => _datasetRoot;

    private IReadOnlyList<SeedCaseDefinition> LoadCases()
    {
        var rawCasesRoot = Path.Combine(_datasetRoot, "00_raw", "txt");
        if (!Directory.Exists(rawCasesRoot))
        {
            throw new DirectoryNotFoundException(
                $"Dataset seed folder was not found at '{rawCasesRoot}'. Configure DatasetSeed:RootPath if needed.");
        }

        var matrixById = LoadCaseMatrix();

        var cases = Directory
            .EnumerateDirectories(rawCasesRoot)
            .Select(directory => Path.GetFileName(directory))
            .Where(caseId => !string.IsNullOrWhiteSpace(caseId))
            .OrderBy(caseId => caseId, StringComparer.OrdinalIgnoreCase)
            .Select(caseId => BuildCaseDefinition(caseId!, rawCasesRoot, matrixById))
            .ToList();

        return cases;
    }

    private SeedCaseDefinition BuildCaseDefinition(
        string caseId,
        string rawCasesRoot,
        IReadOnlyDictionary<string, CaseMatrixEntry> matrixById)
    {
        string applicationPath = Path.Combine(rawCasesRoot, caseId, "loan_application.txt");
        if (!File.Exists(applicationPath))
        {
            throw new FileNotFoundException($"Missing loan application seed file for case '{caseId}'.", applicationPath);
        }

        ParsedLoanApplication parsed = LoanApplicationTextParser.Parse(File.ReadAllText(applicationPath), caseId);
        matrixById.TryGetValue(caseId, out CaseMatrixEntry? matrixEntry);

        string expectedOutcome = MapExpectedOutcome(matrixEntry?.Decision);
        string borrower = matrixEntry?.Borrower ?? parsed.BorrowerName;
        string title = $"{borrower} ({caseId})";
        string propertySummary = BuildPropertySummary(parsed, matrixEntry);
        string description =
            $"Mortgage application {caseId} for {borrower}. {propertySummary} Requested loan {FormatCurrency(parsed.RequestedLoanAmount)}.";

        return new SeedCaseDefinition(
            CaseId: caseId,
            BorrowerName: borrower,
            CoBorrowerName: matrixEntry?.CoBorrower ?? parsed.CoBorrowerName,
            RequestedLoanAmount: parsed.RequestedLoanAmount > 0 ? parsed.RequestedLoanAmount : matrixEntry?.RequestedLoan ?? 0m,
            PurchasePrice: parsed.PurchasePrice ?? matrixEntry?.PurchasePrice,
            MonthlyIncome: parsed.MonthlyIncome ?? matrixEntry?.MonthlyIncome,
            MonthlyDebt: parsed.MonthlyDebt ?? matrixEntry?.MonthlyDebt,
            PropertyAddress: matrixEntry?.PropertyAddress ?? parsed.PropertyAddress,
            PropertyCityState: parsed.PropertyCityState,
            PropertyType: matrixEntry?.PropertyType ?? parsed.PropertyType,
            Product: parsed.Product,
            Purpose: parsed.Purpose,
            ExpectedOutcome: expectedOutcome,
            PrimaryReason: matrixEntry?.PrimaryReason,
            OccupationTitle: matrixEntry?.Title,
            Description: description.Trim(),
            DemoTagline: BuildDemoTagline(parsed, matrixEntry));
    }

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
        string matrixPath = Path.Combine(_datasetRoot, "case_matrix.json");
        if (!File.Exists(matrixPath))
        {
            return new Dictionary<string, CaseMatrixEntry>(StringComparer.OrdinalIgnoreCase);
        }

        CaseMatrixEntry[]? entries = JsonSerializer.Deserialize<CaseMatrixEntry[]>(File.ReadAllText(matrixPath), MatrixJsonOptions);
        return entries?
                   .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
                   .ToDictionary(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
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

    private sealed class CaseMatrixEntry
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("borrower")]
        public string? Borrower { get; set; }

        [JsonPropertyName("coborrower")]
        public string? CoBorrower { get; set; }

        [JsonPropertyName("requestedLoan")]
        public decimal? RequestedLoan { get; set; }

        [JsonPropertyName("purchasePrice")]
        public decimal? PurchasePrice { get; set; }

        [JsonPropertyName("monthlyIncome")]
        public decimal? MonthlyIncome { get; set; }

        [JsonPropertyName("monthlyDebt")]
        public decimal? MonthlyDebt { get; set; }

        [JsonPropertyName("propertyAddress")]
        public string? PropertyAddress { get; set; }

        [JsonPropertyName("propertyType")]
        public string? PropertyType { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("decision")]
        public string? Decision { get; set; }

        [JsonPropertyName("primaryReason")]
        public string? PrimaryReason { get; set; }

        [JsonPropertyName("score")]
        public int? Score { get; set; }
    }
}
