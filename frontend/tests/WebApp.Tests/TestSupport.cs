using Cohere.LoanProcessing.WebApp.Configuration;
using Cohere.LoanProcessing.WebApp.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Cohere.LoanProcessing.WebApp.Tests;

internal static class TestSupport
{
    internal static string WebAppContentRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WebApp"));

    internal static string DatasetSeedRoot =>
        FindDatasetSeedRoot(WebAppContentRoot);

    internal static DatasetSeedCatalogService CreateCatalog() =>
        new(CreateWebHostEnvironment(), Options.Create(new DatasetSeedOptions { RootPath = DatasetSeedRoot }));

    internal static TestWebHostEnvironment CreateWebHostEnvironment() =>
        new()
        {
            ContentRootPath = WebAppContentRoot
        };

    private static string FindDatasetSeedRoot(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current is not null)
        {
            string candidate = Path.Combine(current.FullName, "dataset-seed");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate dataset-seed folder for tests.");
    }

    internal sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Cohere.LoanProcessing.WebApp.Tests";

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = "Development";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
