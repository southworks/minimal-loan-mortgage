using LoanWorkflow.Mcp.Options;
using Microsoft.Extensions.Options;

namespace LoanWorkflow.Mcp.Adapters;

public sealed class LocalCaseDataStore : ICaseDataStore
{
    private readonly string _datasetRootPath;
    private readonly DatasetOptions _datasetOptions;
    private readonly CaseCatalog _caseCatalog;

    public LocalCaseDataStore(
        IOptions<DatasetOptions> options,
        IHostEnvironment environment,
        CaseCatalog caseCatalog)
    {
        _datasetOptions = options.Value;
        _caseCatalog = caseCatalog;
        _datasetRootPath = CasePathResolver.ResolveDatasetRoot(environment.ContentRootPath, _datasetOptions.RootPath);
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
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Case document not found: {path}", path);
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    public Task<IReadOnlyList<string>> ListDocumentsAsync(string caseId, EvidenceCategory category, CancellationToken cancellationToken = default)
    {
        ValidateCaseId(caseId);
        string normalizedCaseId = _caseCatalog.NormalizeCaseId(caseId);
        var dir = CategoryDirectory(normalizedCaseId, category);
        if (!Directory.Exists(dir))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var files = Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => name is not null && !name.StartsWith("SCHEMA", StringComparison.OrdinalIgnoreCase))
            .Cast<string>()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    private string CategoryDirectory(string caseId, EvidenceCategory category) =>
        CasePathResolver.GetCategoryDirectory(_datasetRootPath, _datasetOptions, caseId, category);

    private string FilePath(string caseId, EvidenceCategory category, string fileName) =>
        Path.Combine(CategoryDirectory(caseId, category), fileName);

    private static void ValidateCaseId(string caseId)
    {
        if (string.IsNullOrWhiteSpace(caseId))
        {
            throw new ArgumentException("Case id must be provided.", nameof(caseId));
        }
    }
}
