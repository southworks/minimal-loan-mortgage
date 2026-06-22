using Cohere.LoanProcessing.Shared.Contracts.Api.Cases;
using Cohere.LoanProcessing.WebApp.Contracts.Backend;
using Cohere.LoanProcessing.WebApp.Models;

namespace Cohere.LoanProcessing.WebApp.Services;

public sealed class CaseSessionStore
{
    private readonly Dictionary<string, CaseSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<CaseSummaryResponse> GetSummaries() =>
        _sessions.Values
            .Select(session => BackendWorkflowMapper.ToSummary(session))
            .OrderByDescending(summary => summary.UpdatedAt)
            .ToList();

    public CaseSession GetOrCreate(SeedCaseDefinition seedCase)
    {
        if (_sessions.TryGetValue(seedCase.CaseId, out CaseSession? existing))
        {
            return existing;
        }

        var session = CaseSession.Create(seedCase);
        _sessions[seedCase.CaseId] = session;
        return session;
    }

    public CaseSession? TryGet(string caseId) =>
        _sessions.TryGetValue(caseId, out CaseSession? session) ? session : null;

    public void ApplyWorkflowStatus(CaseSession session, BasicWorkflowStatusResponse status) =>
        BackendWorkflowMapper.ApplyWorkflowStatus(session, status);

    public void ApplyDocuments(CaseSession session, CaseDocumentsResponse documents) =>
        BackendWorkflowMapper.ApplyDocuments(session, documents);

    public void ApplyHumanDecision(CaseSession session, bool approved, string? notes) =>
        BackendWorkflowMapper.ApplyHumanDecision(session, approved, notes);
}

public sealed class CaseSession
{
    public required SeedCaseDefinition SeedCase { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string? ExecutionId { get; set; }

    public string BackendStatus { get; set; } = "NotStarted";

    public string? FailureReason { get; set; }

    public DateTimeOffset? LastWorkflowUpdateUtc { get; set; }

    public IReadOnlyList<DocumentRecordDto> Documents { get; set; } = [];

    public DocumentProcessingResultDto? DocumentProcessing { get; set; }

    public UnderwritingResultDto? Underwriting { get; set; }

    public ResponsibleAiResultDto? ResponsibleAi { get; set; }

    public LoanSetupResultDto? LoanSetup { get; set; }

    public HumanDecisionDto? HumanDecision { get; set; }

    public List<CaseNoteDto> Notes { get; } = [];

    public static CaseSession Create(SeedCaseDefinition seedCase)
    {
        var now = DateTimeOffset.UtcNow;
        return new CaseSession
        {
            SeedCase = seedCase,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
