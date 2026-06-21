using System.Globalization;
using LoanWorkflow.Mcp.Models;

namespace LoanWorkflow.Mcp.Adapters;

public static class ApplicationProfileParser
{
    public static ApplicationProfile Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return new ApplicationProfile
        {
            LoanProduct = ReadLabel(text, "Product"),
            LoanPurpose = ReadLabel(text, "Purpose"),
            OccupancyType = ReadLabel(text, "Occupancy Type"),
            RequestedLoanAmount = ReadMoney(text, "Requested Amount"),
            PurchasePrice = ReadMoney(text, "Purchase Price"),
            DownPayment = ReadMoney(text, "Down Payment"),
            DeclaredMonthlyIncome = ReadMoney(text, "Monthly Income"),
            ExistingMonthlyDebt = ReadMoney(text, "Monthly Debt")
        };
    }

    public static bool LooksLikeApplicationDocument(string text) =>
        text.Contains("LOAN REQUEST", StringComparison.OrdinalIgnoreCase)
        || text.Contains("loan_application", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Requested Amount", StringComparison.OrdinalIgnoreCase)
        || text.Contains("UNIFORM RESIDENTIAL LOAN APPLICATION", StringComparison.OrdinalIgnoreCase);

    private static string? ReadLabel(string text, string label)
    {
        foreach (string line in text.Split('\n', StringSplitOptions.TrimEntries))
        {
            int separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            string lineLabel = line[..separatorIndex].Trim();
            if (!lineLabel.Equals(label, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return line[(separatorIndex + 1)..].Trim();
        }

        return null;
    }

    private static decimal? ReadMoney(string text, string label)
    {
        string? raw = ReadLabel(text, label);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string normalized = raw
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Trim();

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
            ? value
            : null;
    }
}
