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

        var path = FilePath(category, fileName);
        if (!File.Exists(path) || !fileName.StartsWith($"{caseId}_", StringComparison.Ordinal))
        {
            throw new FileNotFoundException($"Case document not found: {path}", path);
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    public Task<IReadOnlyList<string>> ListDocumentsAsync(string caseId, EvidenceCategory category, CancellationToken cancellationToken = default)
    {
        ValidateCaseId(caseId);
        var dir = CategoryDirectory(category);
        if (!Directory.Exists(dir))
        {
            throw new KeyNotFoundException($"Case category directory not found: {dir}");
        }

        var prefix = $"{caseId}_";
        var files = Directory.EnumerateFiles(dir, $"{prefix}*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => name is not null && !name.StartsWith("SCHEMA", StringComparison.OrdinalIgnoreCase))
            .Cast<string>()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    private string CategoryDirectory(EvidenceCategory category) =>
        Path.Combine(_rootPath, EvidenceCategoryFolders.For(category));

    private string FilePath(EvidenceCategory category, string fileName) =>
        Path.Combine(CategoryDirectory(category), fileName);

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
