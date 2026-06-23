namespace Cohere.LoanProcessing.Shared.Contracts.Api.Health;

public sealed record DependencyHealthDto(
    string Name,
    string Status,
    string? Detail);

public sealed record AgentRuntimeStatusDto(
    string ConfiguredMode,
    string EffectiveMode,
    bool IsDegraded,
    string Detail);

public sealed record ReadinessResponse(
    string Status,
    string ServiceName,
    IReadOnlyList<DependencyHealthDto> Dependencies,
    AgentRuntimeStatusDto Runtime,
    DateTimeOffset CheckedAtUtc);

public sealed record HealthStatusResponse(string Status);
