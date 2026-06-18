namespace LoanWorkflow.Mcp.Options;

public sealed class DatasetOptions
{
    public const string SectionName = "Dataset";

    public string RootPath { get; set; } = string.Empty;

    public string PolicyFilePath { get; set; } = string.Empty;
}
