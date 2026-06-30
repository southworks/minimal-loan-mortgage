using LoanWorkflow.Mcp.Options;
using Microsoft.Extensions.Options;

namespace CohereLoanAndMortgage.Api.Host.Services;

public sealed class LoadedCaseDocument
{
    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required string SourcePath { get; init; }

    public required string Reference { get; init; }

    public required BinaryData Content { get; init; }

    public required DateTimeOffset LastModifiedUtc { get; init; }
}

public sealed class CaseDocumentInfo
{
    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required string SourcePath { get; init; }

    public required string Reference { get; init; }

    public required DateTimeOffset LastModifiedUtc { get; init; }
}

public sealed class LocalCaseDocumentService
{
    private readonly string _datasetRoot;
    private readonly ILogger<LocalCaseDocumentService> _logger;

    public LocalCaseDocumentService(
        IOptions<DatasetOptions> options,
        IHostEnvironment environment,
        ILogger<LocalCaseDocumentService> logger)
    {
        _logger = logger;
        _datasetRoot = ResolveDatasetRoot(environment.ContentRootPath, options.Value.RootPath);
    }

    public static string GetCaseDirectory(string caseId) => caseId.Trim();

    public Task<IReadOnlyList<CaseDocumentInfo>> ListCaseDocumentsAsync(
        string caseId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<CaseDocumentInfo> documents = ListCaseDocuments(caseId);
        return Task.FromResult(documents);
    }

    public Task<LoadedCaseDocument> GetCaseDocumentAsync(
        string caseId,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(caseId))
        {
            throw new InvalidOperationException("CaseId is required.");
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new InvalidOperationException("SourcePath is required.");
        }

        string normalizedCaseId = caseId.Trim();
        string normalizedSourcePath = sourcePath.Trim();
        string caseDirectory = GetCaseDirectoryPath(normalizedCaseId);

        if (!Directory.Exists(caseDirectory))
        {
            throw new KeyNotFoundException(
                $"Case '{normalizedCaseId}' was not found in dataset assets at '{caseDirectory}'.");
        }

        string resolvedPath = ResolveDocumentPath(caseDirectory, normalizedCaseId, normalizedSourcePath);
        if (!File.Exists(resolvedPath))
        {
            throw new KeyNotFoundException(
                $"Document '{normalizedSourcePath}' was not found for case '{normalizedCaseId}'.");
        }

        return Task.FromResult(LoadDocumentFromFile(resolvedPath, normalizedCaseId));
    }

    public Task<IReadOnlyList<LoadedCaseDocument>> LoadCaseDocumentsAsync(
        string caseId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string caseDirectory = GetCaseDirectoryPath(caseId);
        if (!Directory.Exists(caseDirectory))
        {
            _logger.LogInformation(
                "Case directory not found for case {CaseId} at {CaseDirectory}.",
                caseId,
                caseDirectory);

            return Task.FromResult<IReadOnlyList<LoadedCaseDocument>>([]);
        }

        var documents = Directory
            .EnumerateFiles(caseDirectory, "*.txt", SearchOption.TopDirectoryOnly)
            .Where(path => !string.IsNullOrWhiteSpace(Path.GetFileName(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => LoadDocumentFromFile(path, caseId.Trim()))
            .ToArray();

        _logger.LogInformation(
            "Loaded {DocumentCount} document(s) for case {CaseId} from {CaseDirectory}.",
            documents.Length,
            caseId,
            caseDirectory);

        return Task.FromResult<IReadOnlyList<LoadedCaseDocument>>(documents);
    }

    private IReadOnlyList<CaseDocumentInfo> ListCaseDocuments(string caseId)
    {
        string caseDirectory = GetCaseDirectoryPath(caseId);
        if (!Directory.Exists(caseDirectory))
        {
            _logger.LogInformation(
                "Case directory not found for case {CaseId} at {CaseDirectory}.",
                caseId,
                caseDirectory);

            return [];
        }

        var documents = Directory
            .EnumerateFiles(caseDirectory, "*.txt", SearchOption.TopDirectoryOnly)
            .Where(path => !string.IsNullOrWhiteSpace(Path.GetFileName(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => CreateDocumentInfo(path, caseId.Trim()))
            .ToArray();

        _logger.LogInformation(
            "Listed {DocumentCount} document(s) for case {CaseId} from {CaseDirectory}.",
            documents.Length,
            caseId,
            caseDirectory);

        return documents;
    }

    private string GetCaseDirectoryPath(string caseId) =>
        Path.Combine(_datasetRoot, "cases", GetCaseDirectory(caseId), "ingest");

    private static CaseDocumentInfo CreateDocumentInfo(string filePath, string caseId)
    {
        string fileName = Path.GetFileName(filePath);
        string sourcePath = BuildSourcePath(caseId, fileName);

        return new CaseDocumentInfo
        {
            FileName = fileName,
            ContentType = ResolveContentType(fileName),
            SourcePath = sourcePath,
            Reference = filePath,
            LastModifiedUtc = File.GetLastWriteTimeUtc(filePath)
        };
    }

    private static LoadedCaseDocument LoadDocumentFromFile(string filePath, string caseId)
    {
        string fileName = Path.GetFileName(filePath);
        byte[] content = File.ReadAllBytes(filePath);

        return new LoadedCaseDocument
        {
            FileName = fileName,
            ContentType = ResolveContentType(fileName),
            SourcePath = BuildSourcePath(caseId, fileName),
            Reference = filePath,
            Content = BinaryData.FromBytes(content),
            LastModifiedUtc = File.GetLastWriteTimeUtc(filePath)
        };
    }

    private static string ResolveDocumentPath(string caseDirectory, string caseId, string sourcePath)
    {
        string normalizedSourcePath = sourcePath.Replace('\\', '/').Trim();
        string expectedPrefix = $"{GetCaseDirectory(caseId)}/";

        if (normalizedSourcePath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string relativeFileName = normalizedSourcePath[expectedPrefix.Length..];
            return Path.Combine(caseDirectory, relativeFileName);
        }

        if (normalizedSourcePath.Contains('/'))
        {
            throw new InvalidOperationException(
                $"SourcePath '{sourcePath}' does not belong to case '{caseId}'.");
        }

        return Path.Combine(caseDirectory, normalizedSourcePath);
    }

    private static string BuildSourcePath(string caseId, string fileName) =>
        $"{GetCaseDirectory(caseId)}/{fileName}";

    private static string ResolveContentType(string fileName) =>
        Path.GetExtension(fileName).Equals(".txt", StringComparison.OrdinalIgnoreCase)
            ? "text/plain"
            : "application/octet-stream";

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
}
