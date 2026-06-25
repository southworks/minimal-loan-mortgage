using AgentGovernance;
using AgentGovernance.Extensions.Microsoft.Agents;
using AgentGovernance.Policy;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CohereLoanAndMortgage.Foundry.Governance;

public sealed class FoundryAgentGovernanceBootstrap
{
    private readonly string? _policiesBaseDirectory;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly Dictionary<AgentRole, GovernanceKernel> _kernels = new();
    private readonly Dictionary<AgentRole, AgentFrameworkGovernanceAdapter> _adapters = new();
    private readonly Dictionary<AgentRole, RoguePolicyConfig> _rogueConfigs = new();

    public FoundryAgentGovernanceBootstrap(
        string? policiesBaseDirectory = null,
        ILoggerFactory? loggerFactory = null)
    {
        _policiesBaseDirectory = policiesBaseDirectory;
        _loggerFactory = loggerFactory;
    }

    public GovernanceKernel GetKernel(AgentRole role)
    {
        if (_kernels.TryGetValue(role, out GovernanceKernel? existing))
        {
            return existing;
        }

        FoundryAgentGovernancePolicyPaths.EnsurePolicyFilesExist(role, _policiesBaseDirectory);

        string governancePath = FoundryAgentGovernancePolicyPaths.ResolveGovernancePolicyPath(role, _policiesBaseDirectory);
        var kernel = new GovernanceKernel(new GovernanceOptions
        {
            PolicyPaths = [governancePath],
            ConflictStrategy = ConflictResolutionStrategy.DenyOverrides,
            EnableRings = false,
            EnablePromptInjectionDetection = false,
            EnableCircuitBreaker = true
        });

        _kernels[role] = kernel;
        _rogueConfigs[role] = RoguePolicyConfig.Load(role, _policiesBaseDirectory);
        return kernel;
    }

    public AgentFrameworkGovernanceAdapter GetAdapter(AgentRole role)
    {
        if (_adapters.TryGetValue(role, out AgentFrameworkGovernanceAdapter? existing))
        {
            return existing;
        }

        GovernanceKernel kernel = GetKernel(role);
        var adapter = new AgentFrameworkGovernanceAdapter(
            kernel,
            new AgentFrameworkGovernanceOptions
            {
                DefaultAgentId = AgentIdentityCatalog.ToMeshAgentId(role),
                EnableFunctionMiddleware = true
            });

        _adapters[role] = adapter;
        return adapter;
    }

    public RoguePolicyConfig GetRogueConfig(AgentRole role)
    {
        if (_rogueConfigs.TryGetValue(role, out RoguePolicyConfig? existing))
        {
            return existing;
        }

        GetKernel(role);
        return _rogueConfigs[role];
    }

    public AIAgent WrapAgent(AIAgent agent, AgentRole role)
    {
        AgentFrameworkGovernanceAdapter adapter = GetAdapter(role);
        RoguePolicyConfig rogueConfig = GetRogueConfig(role);
        ILogger<RogueToolCallMiddleware>? rogueLogger =
            _loggerFactory?.CreateLogger<RogueToolCallMiddleware>();

        var rogueMiddleware = new RogueToolCallMiddleware(rogueConfig, rogueLogger);

        return agent
            .AsBuilder()
            .WithGovernance(adapter)
            .Use((innerAgent, context, next, cancellationToken) =>
                rogueMiddleware.InvokeAsync(innerAgent, context, next, cancellationToken))
            .Build();
    }
}
