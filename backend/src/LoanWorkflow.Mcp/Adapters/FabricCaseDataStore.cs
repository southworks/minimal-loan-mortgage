using Azure;
using LoanWorkflow.Mcp.Options;
using Microsoft.Extensions.Options;

namespace LoanWorkflow.Mcp.Adapters;

public sealed class FabricCaseDataStore : ICaseDataStore
{
    private readonly IFabricLakehouseClient _client;
    private readonly DatasetOptions _datasetOptions;
    private readonly CaseCatalog _caseCatalog;
    private readonly string _evidenceRoot;

    public FabricCaseDataStore(
        IFabricLakehouseClient client,
        IOptions<DataSourceOptions> dataSourceOptions,
        IOptions<DatasetOptions> datasetOptions,
        CaseCatalog caseCatalog)
    {
        _client = client;
        _datasetOptions = datasetOptions.Value;
        _caseCatalog = caseCatalog;
        _evidenceRoot = string.IsNullOrWhiteSpace(dataSourceOptions.Value.FabricLakehouse?.EvidenceRoot)
            ? "Files/bronze"
            : dataSourceOptions.Value.FabricLakehouse!.EvidenceRoot;
    }

    public async Task<string> ReadDocumentAsync(string caseId, EvidenceCategory category, string fileName, CancellationToken cancellationToken = default)
    {
        ValidateCaseId(caseId);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name must be provided.", nameof(fileName));
        }

        string normalizedCaseId = _caseCatalog.NormalizeCaseId(caseId);
        var path = FilePath(normalizedCaseId, category, fileName);
        try
        {
            return await _client.ReadFileAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException ex)
        {
            throw new FileNotFoundException($"Case document not found: {path}", ex);
        }
    }

    public async Task<IReadOnlyList<string>> ListDocumentsAsync(string caseId, EvidenceCategory category, CancellationToken cancellationToken = default)
    {
        ValidateCaseId(caseId);
        string normalizedCaseId = _caseCatalog.NormalizeCaseId(caseId);
        var categoryPath = CategoryPath(normalizedCaseId, category);

        IReadOnlyList<string> all;
        try
        {
            all = await _client.ListFilesAsync(categoryPath, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return [];
        }

        return all
            .Select(ExtractFileName)
            .Where(name => name is not null
                           && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                           && !name.StartsWith("SCHEMA", StringComparison.OrdinalIgnoreCase))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    private string CategoryPath(string caseId, EvidenceCategory category)
    {
        string normalizedCaseId = CasePathResolver.NormalizeCaseId(caseId);
        return $"{_evidenceRoot}/{_datasetOptions.CasesRelativePath}/{normalizedCaseId}/{_datasetOptions.FabricPrerequisiteSubfolder}/{EvidenceCategoryFolders.For(category)}";
    }

    private string FilePath(string caseId, EvidenceCategory category, string fileName) =>
        $"{CategoryPath(caseId, category)}/{fileName}";

    private static string? ExtractFileName(string blobPath)
    {
        var lastSlash = blobPath.LastIndexOf('/');
        return lastSlash >= 0 ? blobPath[(lastSlash + 1)..] : blobPath;
    }

    private static void ValidateCaseId(string caseId)
    {
        if (string.IsNullOrWhiteSpace(caseId))
        {
            throw new ArgumentException("Case id must be provided.", nameof(caseId));
        }
    }
}
