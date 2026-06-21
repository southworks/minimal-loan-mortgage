namespace CohereLoanAndMortgage.Api.Host.Options;

public sealed class CaseWorkflowOptions
{
    public const string SectionName = "CaseWorkflow";

    /// <summary>
    /// When true, the API pre-indexes normalized case documents before starting the workflow.
    /// The document-processing agent may call index_case_documents again; idempotency avoids duplicate work.
    /// </summary>
    public bool PreIndexCaseDocuments { get; set; } = true;
}
