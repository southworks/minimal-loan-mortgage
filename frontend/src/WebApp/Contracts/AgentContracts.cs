namespace Cohere.LoanProcessing.Shared.Contracts.Agents;

public enum EvidenceSourceType
{
    Document,
    Policy,
    Rule,
    Retrieval,
    AgentOutput
}

public enum FlagSeverity
{
    Info,
    Warning,
    High,
    Critical
}

public sealed record EvidenceProvenance(
    int? SelectedRank = null,
    string? RetrievalMode = null,
    bool? RerankApplied = null,
    double? PreRerankScore = null,
    double? PostRerankScore = null,
    string? Citation = null);

public sealed record EvidenceItem(
    EvidenceSourceType SourceType,
    string SourceId,
    string Excerpt,
    double Relevance,
    EvidenceProvenance? Provenance = null);

public sealed record FlagItem(
    string FlagCode,
    string Category,
    string Message,
    FlagSeverity Severity = FlagSeverity.Warning,
    bool RequiresHumanReview = false);

public sealed record RationaleItem(
    string Code,
    string Message,
    FlagSeverity Severity = FlagSeverity.Info);

public sealed record RetrievalSummaryDto(
    string RetrievalMode,
    string QueryText,
    int CandidateCount,
    int SelectedCount,
    bool RerankApplied,
    bool UsedFallback,
    string? ProductType = null);

public sealed record RetrievalCandidateDto(
    int Rank,
    string Title,
    double MatchScore,
    double? PreRerankScore = null);

public sealed record RetrievalEventDto(
    string ToolName,
    string RetrievalMode,
    string QueryText,
    int CandidateCount,
    int SelectedCount,
    bool RerankApplied,
    bool UsedFallback,
    IReadOnlyList<RetrievalCandidateDto> SelectedCandidates);
