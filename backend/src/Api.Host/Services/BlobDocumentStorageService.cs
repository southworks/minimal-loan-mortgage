using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CohereLoanAndMortgage.Api.Host.Options;
using Microsoft.Extensions.Options;

namespace CohereLoanAndMortgage.Api.Host.Services;

public sealed class StoredDocumentReference
{
    public required string Reference { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required DateTimeOffset UploadedAtUtc { get; init; }
}

public sealed class BlobDocumentStorageService
{
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

    public async Task<StoredDocumentReference> UploadAsync(
        string caseId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        string blobName = $"{caseId}/{Guid.NewGuid():N}/{Path.GetFileName(fileName)}";
        BlobClient blobClient = _containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(
            content,
            new BlobHttpHeaders { ContentType = contentType },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Uploaded document {FileName} for case {CaseId} to {BlobUri}.", fileName, caseId, blobClient.Uri);

        return new StoredDocumentReference
        {
            Reference = blobClient.Uri.ToString(),
            FileName = fileName,
            ContentType = contentType,
            UploadedAtUtc = DateTimeOffset.UtcNow
        };
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
