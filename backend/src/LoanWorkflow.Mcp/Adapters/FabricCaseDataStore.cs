using Azure;
using LoanWorkflow.Mcp.Options;
using Microsoft.Extensions.Options;

namespace LoanWorkflow.Mcp.Adapters;

public sealed class FabricCaseDataStore : ICaseDataStore
{
    private readonly IFabricLakehouseClient _client;
    private readonly string _evidenceRoot;

    public FabricCaseDataStore(IFabricLakehouseClient client, IOptions<DataSourceOptions> options)
    {
        _client = client;
        _evidenceRoot = string.IsNullOrWhiteSpace(options.Value.FabricLakehouse?.EvidenceRoot)
            ? "Files/bronze"
            : options.Value.FabricLakehouse!.EvidenceRoot;
    }

    public async Task<string> ReadDocumentAsync(string caseId, EvidenceCategory category, string fileName, CancellationToken cancellationToken = default)
    {
        ValidateCaseId(caseId);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name must be provided.", nameof(fileName));
        }

        if (!fileName.StartsWith($"{caseId}_", StringComparison.Ordinal))
        {
            throw new FileNotFoundException(
                $"Case document not found: file '{fileName}' does not belong to case '{caseId}'.",
                fileName);
        }

        var path = FilePath(category, fileName);
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
        var categoryFolder = EvidenceCategoryFolders.For(category);

        IReadOnlyList<string> all;
        try
        {
            all = await _client.ListFilesAsync(_evidenceRoot, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new KeyNotFoundException($"Case evidence root not found: {_evidenceRoot}", ex);
        }

        var prefix = $"{caseId}_";
        return all
            .Where(path => PathBelongsToCategory(path, categoryFolder)
                        && IsCaseDocument(path, prefix))
            .Select(ExtractFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    private string FilePath(EvidenceCategory category, string fileName) =>
        $"{_evidenceRoot}/{EvidenceCategoryFolders.For(category)}/{fileName}";

    private static bool PathBelongsToCategory(string fullPath, string categoryFolder)
    {
        var lastSlash = fullPath.LastIndexOf('/');
        if (lastSlash < 0)
        {
            return false;
        }
        var parent = fullPath[..lastSlash];
        var parentLastSlash = parent.LastIndexOf('/');
        var parentFolder = parentLastSlash >= 0 ? parent[(parentLastSlash + 1)..] : parent;
        return string.Equals(parentFolder, categoryFolder, StringComparison.Ordinal);
    }

    private static string? ExtractFileName(string blobPath)
    {
        var lastSlash = blobPath.LastIndexOf('/');
        return lastSlash >= 0 ? blobPath[(lastSlash + 1)..] : blobPath;
    }

    private static bool IsCaseDocument(string fullPath, string caseIdPrefix)
    {
        var fileName = ExtractFileName(fullPath);
        if (fileName is null)
        {
            return false;
        }
        if (fileName.StartsWith("SCHEMA", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return fileName.StartsWith(caseIdPrefix, StringComparison.Ordinal);
    }

    private static void ValidateCaseId(string caseId)
    {
        if (string.IsNullOrWhiteSpace(caseId))
        {
            throw new ArgumentException("Case id must be provided.", nameof(caseId));
        }
    }
}
