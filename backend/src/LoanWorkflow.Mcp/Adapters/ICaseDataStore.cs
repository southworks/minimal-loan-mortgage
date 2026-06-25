namespace LoanWorkflow.Mcp.Adapters;

public interface ICaseDataStore
{
    Task<string> ReadDocumentAsync(string caseId, EvidenceCategory category, string fileName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListDocumentsAsync(string caseId, EvidenceCategory category, CancellationToken cancellationToken = default);
}
