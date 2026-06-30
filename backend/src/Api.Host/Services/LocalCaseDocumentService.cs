using LoanWorkflow.Mcp.Adapters;
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
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".pdf", ".png", ".jpg", ".jpeg"
    };

    private readonly string _datasetRoot;
    private readonly DatasetOptions _datasetOptions;
    private readonly ILogger<LocalCaseDocumentService> _logger;

    public LocalCaseDocumentService(
        IOptions<DatasetOptions> options,
        IHostEnvironment environment,
        ILogger<LocalCaseDocumentService> logger)
    {
        _logger = logger;
        _datasetOptions = options.Value;
        _datasetRoot = CasePathResolver.ResolveDatasetRoot(environment.ContentRootPath, _datasetOptions.RootPath);
    }

    public static string GetCaseDirectory(string caseId) => caseId.Trim();

    public string GetCaseIngestRelativePath(string caseId) =>
        CasePathResolver.GetIngestRelativePath(_datasetOptions, caseId);

    public Task<IReadOnlyList<CaseDocumentInfo>> ListCaseDocumentsAsync(
        string caseId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<CaseDocumentInfo> documents = ListCaseDocuments(RequireCaseId(caseId));
        return Task.FromResult(documents);
    }

    public Task<LoadedCaseDocument> GetCaseDocumentAsync(
        string caseId,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        caseId = RequireCaseId(caseId);

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new InvalidOperationException("SourcePath is required.");
        }

        string normalizedSourcePath = sourcePath.Trim();
        string caseDirectory = GetCaseIngestDirectoryPath(caseId);

        if (!Directory.Exists(caseDirectory))
        {
            throw new KeyNotFoundException(
                $"Case '{caseId}' was not found in dataset assets at '{caseDirectory}'.");
        }

        string resolvedPath = ResolveDocumentPath(caseDirectory, caseId, normalizedSourcePath);
        if (!File.Exists(resolvedPath))
        {
            throw new KeyNotFoundException(
                $"Document '{normalizedSourcePath}' was not found for case '{caseId}'.");
        }

        return Task.FromResult(LoadDocumentFromFile(resolvedPath, caseId, caseDirectory));
    }

    public Task<IReadOnlyList<LoadedCaseDocument>> LoadCaseDocumentsAsync(
        string caseId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        caseId = RequireCaseId(caseId);
        string caseDirectory = GetCaseIngestDirectoryPath(caseId);
        if (!Directory.Exists(caseDirectory))
        {
            _logger.LogInformation(
                "Case ingest directory not found for case {CaseId} at {CaseDirectory}.",
                caseId,
                caseDirectory);

            return Task.FromResult<IReadOnlyList<LoadedCaseDocument>>([]);
        }

        var documents = EnumerateIngestFiles(caseDirectory)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => LoadDocumentFromFile(path, caseId, caseDirectory))
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
        string caseDirectory = GetCaseIngestDirectoryPath(caseId);
        if (!Directory.Exists(caseDirectory))
        {
            _logger.LogInformation(
                "Case ingest directory not found for case {CaseId} at {CaseDirectory}.",
                caseId,
                caseDirectory);

            return [];
        }

        var documents = EnumerateIngestFiles(caseDirectory)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => CreateDocumentInfo(path, caseId, caseDirectory))
            .ToArray();

        _logger.LogInformation(
            "Listed {DocumentCount} document(s) for case {CaseId} from {CaseDirectory}.",
            documents.Length,
            caseId,
            caseDirectory);

        return documents;
    }

    private string GetCaseIngestDirectoryPath(string caseId) =>
        CasePathResolver.GetIngestDirectory(_datasetRoot, _datasetOptions, caseId);

    private static string RequireCaseId(string caseId)
    {
        if (string.IsNullOrWhiteSpace(caseId))
        {
            throw new InvalidOperationException("CaseId is required.");
        }

        return caseId.Trim();
    }

    private static IEnumerable<string> EnumerateIngestFiles(string caseDirectory) =>
        Directory
            .EnumerateFiles(caseDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)));

    private static CaseDocumentInfo CreateDocumentInfo(string filePath, string caseId, string caseDirectory)
    {
        string fileName = Path.GetFileName(filePath);
        string sourcePath = BuildSourcePath(caseId, caseDirectory, filePath);

        return new CaseDocumentInfo
        {
            FileName = fileName,
            ContentType = ResolveContentType(fileName),
            SourcePath = sourcePath,
            Reference = filePath,
            LastModifiedUtc = File.GetLastWriteTimeUtc(filePath)
        };
    }

    private static LoadedCaseDocument LoadDocumentFromFile(string filePath, string caseId, string caseDirectory)
    {
        string fileName = Path.GetFileName(filePath);
        byte[] content = File.ReadAllBytes(filePath);

        return new LoadedCaseDocument
        {
            FileName = fileName,
            ContentType = ResolveContentType(fileName),
            SourcePath = BuildSourcePath(caseId, caseDirectory, filePath),
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
            string relativePath = normalizedSourcePath[expectedPrefix.Length..];
            return Path.Combine(caseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        if (normalizedSourcePath.Contains('/'))
        {
            throw new InvalidOperationException(
                $"SourcePath '{sourcePath}' does not belong to case '{caseId}'.");
        }

        return Path.Combine(caseDirectory, normalizedSourcePath);
    }

    private static string BuildSourcePath(string caseId, string caseDirectory, string filePath)
    {
        string relativePath = Path.GetRelativePath(caseDirectory, filePath).Replace('\\', '/');
        return $"{GetCaseDirectory(caseId)}/{relativePath}";
    }

    private static string ResolveContentType(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".txt" => "text/plain",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
    }
}
