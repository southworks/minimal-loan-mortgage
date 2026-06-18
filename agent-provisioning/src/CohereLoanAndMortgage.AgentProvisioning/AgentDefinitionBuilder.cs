using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.AI.Projects;
using CohereLoanAndMortgage.AgentProvisioning.Models;

namespace CohereLoanAndMortgage.AgentProvisioning;

public sealed class AgentDefinitionBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    public string BuildDefinitionJson(
        AgentAssetBundle bundle,
        ProvisioningSettings settings,
        IReadOnlyDictionary<string, AIProjectConnection> connections)
    {
        JsonArray tools = [];

        foreach (McpDependency dependency in bundle.Mcp.Dependencies)
        {
            if (!connections.TryGetValue(dependency.ConnectionName, out AIProjectConnection? connection))
            {
                if (dependency.Required)
                {
                    throw new InvalidOperationException(
                        $"Agent '{bundle.Manifest.Name}' requires MCP connection '{dependency.ConnectionName}', but it was not resolved.");
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(connection.Target))
            {
                throw new InvalidOperationException(
                    $"MCP connection '{dependency.ConnectionName}' does not define a target URL.");
            }

            tools.Add(new JsonObject
            {
                ["type"] = "mcp",
                ["server_label"] = dependency.ServerLabel,
                ["server_url"] = connection.Target,
                ["project_connection_id"] = connection.Id
            });
        }

        if (bundle.MemoryPolicy.Enabled)
        {
            tools.Add(new JsonObject
            {
                ["type"] = "memory_search_preview",
                ["memory_store_name"] = settings.MemoryStoreName,
                ["scope"] = "{{$userId}}"
            });
        }

        JsonObject definition = new()
        {
            ["kind"] = "prompt",
            ["model"] = settings.ModelDeploymentName,
            ["instructions"] = BuildInstructions(bundle),
            ["temperature"] = 0.2,
            ["text"] = new JsonObject
            {
                ["format"] = new JsonObject
                {
                    ["type"] = "json_schema",
                    ["name"] = "AgentStructuredOutput",
                    ["description"] = "Structured agent output consumed by the loan workflow API.",
                    ["schema"] = JsonNode.Parse(bundle.OutputSchemaJson),
                    ["strict"] = true
                }
            }
        };

        if (tools.Count > 0)
        {
            definition["tools"] = tools;
        }

        return definition.ToJsonString(SerializerOptions);
    }

    public string BuildCreateVersionRequestJson(string definitionJson)
    {
        JsonObject request = new()
        {
            ["definition"] = JsonNode.Parse(definitionJson)
        };

        return request.ToJsonString(SerializerOptions);
    }

    public string ComputeFingerprint(string definitionJson)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(definitionJson));
        return Convert.ToHexString(hash);
    }

    private static string BuildInstructions(AgentAssetBundle bundle)
    {
        StringBuilder builder = new();
        builder.AppendLine(bundle.Instructions);
        builder.AppendLine();
        builder.AppendLine("## Structured Output Contract");
        builder.AppendLine("Return JSON only with these properties: summary, decision, evidence, memoryUpdates.");
        builder.AppendLine("The API requires summary, decision, and evidence.");
        builder.AppendLine();
        builder.AppendLine("## Allowed Decision Values");
        foreach (string decision in bundle.Manifest.AllowedDecisions)
        {
            builder.AppendLine($"- {decision}");
        }

        builder.AppendLine();
        builder.AppendLine("## Memory Participation");
        if (bundle.MemoryPolicy.Enabled)
        {
            builder.AppendLine("You may include optional memoryUpdates for durable reusable observations only.");
            builder.AppendLine("Allowed writes:");
            foreach (string allowed in bundle.MemoryPolicy.AllowedWrites)
            {
                builder.AppendLine($"- {allowed}");
            }

            builder.AppendLine("Forbidden writes:");
            foreach (string forbidden in bundle.MemoryPolicy.ForbiddenWrites)
            {
                builder.AppendLine($"- {forbidden}");
            }
        }
        else
        {
            builder.AppendLine("Return memoryUpdates as an empty array.");
        }

        builder.AppendLine();
        builder.AppendLine("## Workflow Boundaries");
        builder.AppendLine("Consume prior workflow outputs when provided. Do not repeat work owned by earlier agents.");
        builder.AppendLine("Produce recommendations and evidence only. Human-in-the-loop orchestration is handled by the workflow, not by this agent.");

        return builder.ToString().Trim();
    }
}
