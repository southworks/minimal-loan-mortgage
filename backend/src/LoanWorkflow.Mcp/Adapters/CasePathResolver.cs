using LoanWorkflow.Mcp.Options;

namespace LoanWorkflow.Mcp.Adapters;

public static class CasePathResolver
{
    public static string NormalizeCaseId(string caseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        return caseId.Trim();
    }

    public static string GetCaseDirectory(string datasetRootPath, DatasetOptions options, string caseId) =>
        Path.Combine(
            datasetRootPath,
            options.CasesRelativePath,
            NormalizeCaseId(caseId));

    public static string GetIngestDirectory(string datasetRootPath, DatasetOptions options, string caseId) =>
        Path.Combine(
            GetCaseDirectory(datasetRootPath, options, caseId),
            options.IngestSubfolder);

    public static string GetFabricPrerequisiteDirectory(string datasetRootPath, DatasetOptions options, string caseId) =>
        Path.Combine(
            GetCaseDirectory(datasetRootPath, options, caseId),
            options.FabricPrerequisiteSubfolder);

    public static string GetCategoryDirectory(
        string datasetRootPath,
        DatasetOptions options,
        string caseId,
        EvidenceCategory category) =>
        Path.Combine(
            GetFabricPrerequisiteDirectory(datasetRootPath, options, caseId),
            EvidenceCategoryFolders.For(category));

    public static string GetIngestRelativePath(DatasetOptions options, string caseId) =>
        $"{options.CasesRelativePath}/{NormalizeCaseId(caseId)}/{options.IngestSubfolder}";

    public static string ResolveDatasetRoot(string contentRootPath, string? configuredRoot)
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

    public static string ResolveContentPath(string contentRootPath, string path)
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
