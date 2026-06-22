using System.Text;
using System.Text.Json;
using LoanWorkflow.Mcp.Models;

namespace LoanWorkflow.Mcp.Adapters;

public sealed partial class CaseDataAdapter
{
    private static readonly IReadOnlyList<EvidenceCategory> Categories =
        Enum.GetValues<EvidenceCategory>();

    private readonly ICaseDataStore _dataStore;

    public CaseDataAdapter(ICaseDataStore dataStore)
    {
        _dataStore = dataStore;
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

        foreach (var category in Categories)
        {
            IReadOnlyList<string> files;
            try
            {
                files = await _dataStore.ListDocumentsAsync(caseId, category, cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                continue;
            }

            if (files.Count == 0)
            {
                continue;
            }

            var categoryName = category.ToString().ToLowerInvariant();
            availableCategories.Add(categoryName);

            foreach (var fileName in files.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                var content = await _dataStore.ReadDocumentAsync(caseId, category, fileName, cancellationToken);
                using var document = JsonDocument.Parse(content);
                var root = document.RootElement.Clone();

                documents.Add(new CaseDocument
                {
                    DocumentId = root.TryGetProperty("document_id", out var documentId)
                        ? documentId.GetString() ?? Path.GetFileNameWithoutExtension(fileName)
                        : Path.GetFileNameWithoutExtension(fileName),
                    DocumentType = root.TryGetProperty("document_type", out var documentType)
                        ? documentType.GetString() ?? categoryName
                        : categoryName,
                    Category = categoryName,
                    SourcePath = fileName,
                    Content = root,
                    SummaryText = BuildSummaryText(categoryName, root)
                });
            }
        }

        var missingCategories = Categories
            .Select(category => category.ToString().ToLowerInvariant())
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
}
