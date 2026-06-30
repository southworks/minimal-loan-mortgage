namespace LoanWorkflow.Mcp.Options;

public sealed class DatasetOptions
{
    public const string SectionName = "Dataset";

    public string RootPath { get; set; } = string.Empty;

    public string PolicyFilePath { get; set; } = string.Empty;

    public string CasesRelativePath { get; set; } = "cases";

    public string IngestSubfolder { get; set; } = "ingest";

    public string FabricPrerequisiteSubfolder { get; set; } = "fabric-pre-requisite-data";
}
