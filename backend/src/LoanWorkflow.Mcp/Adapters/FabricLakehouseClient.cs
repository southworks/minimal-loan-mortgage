using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Files.DataLake;
using LoanWorkflow.Mcp.Options;
using Microsoft.Extensions.Logging;

namespace LoanWorkflow.Mcp.Adapters;

public sealed class FabricLakehouseClient : IFabricLakehouseClient, IDisposable
{
    private const string FabricOneLakeEndpoint = "https://onelake.dfs.fabric.microsoft.com";
    private const string FabricBlobEndpoint = "https://onelake.blob.fabric.microsoft.com";

    private readonly DataLakeFileSystemClient _fileSystemClient;
    private readonly string _fileSystemName;
    private readonly string _workspaceId;
    private readonly DefaultAzureCredential _credential;
    private readonly int _timeoutSeconds;
    private readonly ILogger<FabricLakehouseClient> _logger;

    private FabricLakehouseClient(
        DataLakeFileSystemClient fileSystemClient,
        string fileSystemName,
        string workspaceId,
        DefaultAzureCredential credential,
        int timeoutSeconds,
        ILogger<FabricLakehouseClient> logger)
    {
        _fileSystemClient = fileSystemClient;
        _fileSystemName = fileSystemName;
        _workspaceId = workspaceId;
        _credential = credential;
        _timeoutSeconds = timeoutSeconds;
        _logger = logger;
    }

    public static FabricLakehouseClient Create(DataSourceOptions options, ILogger<FabricLakehouseClient> logger)
    {
        var workspaceId = options.FabricLakehouse?.WorkspaceId;
        var lakehouseId = options.FabricLakehouse?.LakehouseId;
        if (string.IsNullOrWhiteSpace(workspaceId) || string.IsNullOrWhiteSpace(lakehouseId))
        {
            throw new InvalidOperationException(
                "FabricLakehouse:WorkspaceId and FabricLakehouse:LakehouseId are required when DataSource:Mode is Fabric.");
        }

        var credential = new DefaultAzureCredential();
        var serviceUri = new Uri($"{FabricOneLakeEndpoint}/{workspaceId}");
        var serviceClient = new DataLakeServiceClient(serviceUri, credential);
        var fileSystemClient = serviceClient.GetFileSystemClient(lakehouseId);
        var timeoutSeconds = options.FabricLakehouse?.TimeoutSeconds ?? 30;

        logger.LogInformation(
            "FabricLakehouseClient initialized against workspace {WorkspaceId} lakehouse {LakehouseId} timeout {TimeoutSeconds}s.",
            workspaceId,
            lakehouseId,
            timeoutSeconds);

        return new FabricLakehouseClient(fileSystemClient, lakehouseId, workspaceId, credential, timeoutSeconds, logger);
    }

    public async Task<bool> FileExistsAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var (directory, fileName) = SplitPath(relativePath);
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var directoryClient = _fileSystemClient.GetDirectoryClient(directory);
        var fileClient = directoryClient.GetFileClient(fileName);
        try
        {
            var response = await fileClient.ExistsAsync(linkedCts.Token).ConfigureAwait(false);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public async Task<string> ReadFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var blobUri = new Uri($"{FabricBlobEndpoint}/{_workspaceId}/{_fileSystemName}/{relativePath}");
        var blobClient = new BlobClient(blobUri, _credential);
        try
        {
            var response = await blobClient.DownloadContentAsync(linkedCts.Token).ConfigureAwait(false);
            return response.Value.Content.ToString();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new FileNotFoundException(
                $"Fabric file '{relativePath}' was not found in lakehouse '{_fileSystemName}'.",
                ex);
        }
    }

    public async Task<IReadOnlyList<string>> ListFilesAsync(string relativeDirectory, CancellationToken cancellationToken = default)
    {
        var results = new List<string>();
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var enumerable = _fileSystemClient.GetPathsAsync(relativeDirectory, recursive: true, userPrincipalName: false, linkedCts.Token);
        await foreach (var pathItem in enumerable.WithCancellation(linkedCts.Token).ConfigureAwait(false))
        {
            if (pathItem.IsDirectory == true)
            {
                continue;
            }

            var name = pathItem.Name;
            var lastSlash = name.LastIndexOf('/');
            if (lastSlash >= 0)
            {
                name = name[(lastSlash + 1)..];
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                results.Add(name);
            }
        }

        return results;
    }

    private static (string Directory, string FileName) SplitPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Path is required.", nameof(relativePath));
        }

        var trimmed = relativePath.Trim('/');
        var lastSlash = trimmed.LastIndexOf('/');
        if (lastSlash < 0)
        {
            throw new ArgumentException(
                $"Path '{relativePath}' must include a directory and a file name separated by '/'.",
                nameof(relativePath));
        }

        return (trimmed[..lastSlash], trimmed[(lastSlash + 1)..]);
    }

    public void Dispose()
    {
    }
}
