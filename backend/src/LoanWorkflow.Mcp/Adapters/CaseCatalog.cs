using System.Text.Json;
using System.Text.Json.Serialization;
using LoanWorkflow.Mcp.Options;

namespace LoanWorkflow.Mcp.Adapters;

public sealed class CaseCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, CaseCatalogEntry> _byCaseId;
    private readonly Dictionary<string, CaseCatalogEntry> _byLegacyId;

    private CaseCatalog(IReadOnlyList<CaseCatalogEntry> entries)
    {
        _byCaseId = new Dictionary<string, CaseCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        _byLegacyId = new Dictionary<string, CaseCatalogEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (CaseCatalogEntry entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.CaseId))
            {
                continue;
            }

            _byCaseId[entry.CaseId.Trim()] = entry;

            string? legacyId = entry.LegacyId ?? entry.ApplicationId;
            if (!string.IsNullOrWhiteSpace(legacyId))
            {
                _byLegacyId[legacyId.Trim()] = entry;
            }
        }
    }

    public static CaseCatalog Load(string datasetRootPath, DatasetOptions options)
    {
        string catalogPath = Path.Combine(datasetRootPath, options.CasesRelativePath, "catalog.json");
        if (!File.Exists(catalogPath))
        {
            return new CaseCatalog([]);
        }

        CaseCatalogEntry[]? entries = JsonSerializer.Deserialize<CaseCatalogEntry[]>(File.ReadAllText(catalogPath), JsonOptions);
        return new CaseCatalog(entries ?? []);
    }

    public IReadOnlyCollection<string> CaseIds => _byCaseId.Keys;

    public string NormalizeCaseId(string caseOrLegacyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseOrLegacyId);
        string trimmed = caseOrLegacyId.Trim();

        if (_byCaseId.ContainsKey(trimmed))
        {
            return trimmed;
        }

        if (_byLegacyId.TryGetValue(trimmed, out CaseCatalogEntry? entry))
        {
            return entry.CaseId;
        }

        return trimmed;
    }

    public string GetApplicationId(string caseOrLegacyId)
    {
        if (TryGetEntry(caseOrLegacyId, out CaseCatalogEntry? entry))
        {
            return entry.ApplicationId ?? entry.LegacyId ?? entry.CaseId;
        }

        return NormalizeCaseId(caseOrLegacyId);
    }

    public bool TryGetEntry(string caseOrLegacyId, out CaseCatalogEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseOrLegacyId);
        string trimmed = caseOrLegacyId.Trim();

        if (_byCaseId.TryGetValue(trimmed, out entry!))
        {
            return true;
        }

        if (_byLegacyId.TryGetValue(trimmed, out entry!))
        {
            return true;
        }

        entry = null!;
        return false;
    }
}

public sealed class CaseCatalogEntry
{
    [JsonPropertyName("caseId")]
    public string CaseId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("outcomeTag")]
    public string? OutcomeTag { get; set; }

    [JsonPropertyName("legacyId")]
    public string? LegacyId { get; set; }

    [JsonPropertyName("applicationId")]
    public string? ApplicationId { get; set; }

    [JsonPropertyName("context")]
    public CaseCatalogContext? Context { get; set; }
}

public sealed class CaseCatalogContext
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
