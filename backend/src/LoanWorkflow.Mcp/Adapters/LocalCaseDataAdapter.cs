using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LoanWorkflow.Mcp.Models;
using LoanWorkflow.Mcp.Options;
using Microsoft.Extensions.Options;

namespace LoanWorkflow.Mcp.Adapters;

public sealed partial class LocalCaseDataAdapter
{
    private static readonly IReadOnlyDictionary<string, string> CategoryFolders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["identity"] = "02_identity",
        ["income"] = "03_income",
        ["employment"] = "04_employment",
        ["banking"] = "05_banking",
        ["credit"] = "06_credit",
        ["collateral"] = "07_collateral"
    };

    private readonly DatasetOptions _options;

    public LocalCaseDataAdapter(IOptions<DatasetOptions> options, IHostEnvironment environment)
    {
        _options = options.Value;
        _options.RootPath = ResolveContentPath(environment.ContentRootPath, _options.RootPath);
    }

    public async Task<GetCaseDocumentsResponse> GetCaseDocumentsAsync(
        string caseId,
        string executionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

        var documents = new List<CaseDocument>();
        var availableCategories = new List<string>();

        foreach (var (category, folder) in CategoryFolders)
        {
            var folderPath = Path.Combine(_options.RootPath, folder);
            if (!Directory.Exists(folderPath))
            {
                continue;
            }

            var files = Directory.GetFiles(folderPath, $"{caseId}_*.json", SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
            {
                continue;
            }

            availableCategories.Add(category);

            foreach (var file in files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                await using var stream = File.OpenRead(file);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = document.RootElement.Clone();

                documents.Add(new CaseDocument
                {
                    DocumentId = root.TryGetProperty("document_id", out var documentId)
                        ? documentId.GetString() ?? Path.GetFileNameWithoutExtension(file)
                        : Path.GetFileNameWithoutExtension(file),
                    DocumentType = root.TryGetProperty("document_type", out var documentType)
                        ? documentType.GetString() ?? category
                        : category,
                    Category = category,
                    SourcePath = file,
                    Content = root,
                    SummaryText = BuildSummaryText(category, root)
                });
            }
        }

        var missingCategories = CategoryFolders.Keys
            .Except(availableCategories, StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new GetCaseDocumentsResponse
        {
            CaseId = caseId,
            ExecutionId = executionId,
            Documents = documents,
            AvailableCategories = availableCategories,
            MissingCategories = missingCategories
        };
    }

    public static string BuildSummaryText(string category, JsonElement content)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Category: {category}");

        foreach (var property in content.EnumerateObject().OrderBy(property => property.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                builder.AppendLine($"{property.Name}: {property.Value.GetRawText()}");
            }
            else
            {
                builder.AppendLine($"{property.Name}: {property.Value}");
            }
        }

        return builder.ToString().Trim();
    }

    private static string ResolveContentPath(string contentRootPath, string path)
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

public sealed partial class PolicyParser
{
    private static readonly Regex PolicyRefRegex = PolicyRefPattern();

    public IReadOnlyList<PolicyEntry> Parse(string policyText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyText);

        var normalizedText = policyText.Replace("\r\n", "\n", StringComparison.Ordinal);
        var blocks = normalizedText
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(block => block.StartsWith("Policy Ref:", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var entries = new List<PolicyEntry>(blocks.Length);

        foreach (var block in blocks)
        {
            var lines = block.Split('\n', StringSplitOptions.TrimEntries);
            var policyRef = lines[0].Replace("Policy Ref:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            var rule = ExtractValue(lines, "Rule:");
            var threshold = ExtractValue(lines, "Threshold:");
            var action = ExtractValue(lines, "Action:");
            var exception = ExtractValue(lines, "Exception:");

            entries.Add(new PolicyEntry
            {
                PolicyRef = policyRef,
                Rule = rule,
                Threshold = threshold,
                Action = action,
                Exception = exception,
                FullText = block.Trim()
            });
        }

        return entries;
    }

    public static string ComputeContentHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private static string ExtractValue(IEnumerable<string> lines, string prefix)
    {
        var line = lines.FirstOrDefault(value => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return line is null ? string.Empty : line[prefix.Length..].Trim();
    }

    [GeneratedRegex(@"Policy Ref:\s*([A-Z0-9\-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex PolicyRefPattern();
}
