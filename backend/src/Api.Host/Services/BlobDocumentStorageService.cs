using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CohereLoanAndMortgage.Api.Host.Options;
using Microsoft.Extensions.Options;

namespace CohereLoanAndMortgage.Api.Host.Services;

public sealed class LoadedCaseDocument
{
    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required string BlobName { get; init; }

    public required string Reference { get; init; }

    public required BinaryData Content { get; init; }

    public required DateTimeOffset LastModifiedUtc { get; init; }
}

public sealed class BlobDocumentStorageService
{
    private const string CaseRootPrefix = "cases";

    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<BlobDocumentStorageService> _logger;

    public BlobDocumentStorageService(
        IOptions<AzureBlobStorageOptions> options,
        ILogger<BlobDocumentStorageService> logger)
    {
        _logger = logger;
        AzureBlobStorageOptions storageOptions = options.Value;

        BlobServiceClient blobServiceClient = CreateBlobServiceClient(storageOptions);
        _containerClient = blobServiceClient.GetBlobContainerClient(storageOptions.ContainerName);
    }

    public static string GetCasePrefix(string caseId) => $"{CaseRootPrefix}/{caseId.Trim()}/";

    public async Task<IReadOnlyList<LoadedCaseDocument>> LoadCaseDocumentsAsync(
        string caseId,
        CancellationToken cancellationToken)
    {
        string prefix = GetCasePrefix(caseId);
        var documents = new List<LoadedCaseDocument>();

        await foreach (BlobItem blobItem in _containerClient
                           .GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken)
                           .ConfigureAwait(false))
        {
            if (blobItem.Properties.ContentLength == 0)
            {
                continue;
            }

            BlobClient blobClient = _containerClient.GetBlobClient(blobItem.Name);
            BlobDownloadResult download = await blobClient
                .DownloadContentAsync(cancellationToken)
                .ConfigureAwait(false);

            string fileName = Path.GetFileName(blobItem.Name);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            documents.Add(new LoadedCaseDocument
            {
                FileName = fileName,
                ContentType = download.Details.ContentType ?? "application/octet-stream",
                BlobName = blobItem.Name,
                Reference = blobClient.Uri.ToString(),
                Content = download.Content,
                LastModifiedUtc = blobItem.Properties.LastModified ?? DateTimeOffset.UtcNow
            });
        }

        _logger.LogInformation(
            "Loaded {DocumentCount} document(s) for case {CaseId} from prefix {CasePrefix}.",
            documents.Count,
            caseId,
            prefix);

        return documents;
    }

    private static BlobServiceClient CreateBlobServiceClient(AzureBlobStorageOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return new BlobServiceClient(options.ConnectionString);
        }

        if (!string.IsNullOrWhiteSpace(options.BlobServiceUri))
        {
            return new BlobServiceClient(new Uri(options.BlobServiceUri), new Azure.Identity.DefaultAzureCredential());
        }

        throw new InvalidOperationException(
            "Azure Blob Storage is not configured. Set AzureStorage:ConnectionString or AzureStorage:BlobServiceUri.");
    }
}
