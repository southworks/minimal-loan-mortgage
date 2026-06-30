using LoanWorkflow.Mcp.Options;
using Microsoft.Extensions.Options;

namespace LoanWorkflow.Mcp.Adapters;

public sealed class LocalCaseDataStore : ICaseDataStore
{
    private readonly string _rootPath;

    public LocalCaseDataStore(IOptions<DatasetOptions> options, IHostEnvironment environment)
    {
        _rootPath = ResolveContentPath(environment.ContentRootPath, options.Value.RootPath);
    }

    public async Task<string> ReadDocumentAsync(string caseId, EvidenceCategory category, string fileName, CancellationToken cancellationToken = default)
    {
        ValidateCaseId(caseId);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name must be provided.", nameof(fileName));
        }

        var path = FilePath(caseId, category, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Case document not found: {path}", path);
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    public Task<IReadOnlyList<string>> ListDocumentsAsync(string caseId, EvidenceCategory category, CancellationToken cancellationToken = default)
    {
        ValidateCaseId(caseId);
        var dir = CategoryDirectory(caseId, category);
        if (!Directory.Exists(dir))
        {
            throw new KeyNotFoundException($"Case category directory not found: {dir}");
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
        Path.Combine(_rootPath, "cases", caseId.Trim(), "fabric-pre-requisite-data", EvidenceCategoryFolders.For(category));

    private string FilePath(string caseId, EvidenceCategory category, string fileName) =>
        Path.Combine(CategoryDirectory(caseId, category), fileName);

    private static void ValidateCaseId(string caseId)
    {
        if (string.IsNullOrWhiteSpace(caseId))
        {
            throw new ArgumentException("Case id must be provided.", nameof(caseId));
        }
    }

    private static string ResolveContentPath(string contentRootPath, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(contentRootPath, path));
    }
}
