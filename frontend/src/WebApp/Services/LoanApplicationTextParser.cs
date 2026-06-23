using System.Globalization;
using System.Text.RegularExpressions;

namespace Cohere.LoanProcessing.WebApp.Services;

internal static partial class LoanApplicationTextParser
{
    public static ParsedLoanApplication Parse(string content, string caseId)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            Match match = FieldLineRegex().Match(line);
            if (!match.Success)
            {
                continue;
            }

            fields[NormalizeKey(match.Groups["key"].Value)] = match.Groups["value"].Value.Trim();
        }

        return new ParsedLoanApplication(
            CaseId: Read(fields, "applicationid") ?? caseId,
            BorrowerName: Read(fields, "fullname") ?? caseId,
            CoBorrowerName: Read(fields, "coborrowerfullname"),
            RequestedLoanAmount: ParseMoney(Read(fields, "requestedamount")),
            PurchasePrice: ParseMoney(Read(fields, "purchaseprice")),
            MonthlyIncome: ParseMoney(Read(fields, "monthlyincome")),
            MonthlyDebt: ParseMoney(Read(fields, "monthlydebt")),
            PropertyAddress: Read(fields, "address", "propertyaddress"),
            PropertyCityState: Read(fields, "city/state", "citystate"),
            PropertyType: Read(fields, "propertytype"),
            Product: Read(fields, "product"),
            Purpose: Read(fields, "purpose"));
    }

    private static string? Read(IReadOnlyDictionary<string, string> fields, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (fields.TryGetValue(NormalizeKey(key), out string? value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string NormalizeKey(string key) =>
        new(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static decimal ParseMoney(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        string normalized = value
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Trim();

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount)
            ? amount
            : 0m;
    }

    [GeneratedRegex(@"^\s*(?<key>[^:]+?)\s*:\s*(?<value>.+?)\s*$")]
    private static partial Regex FieldLineRegex();
}

internal sealed record ParsedLoanApplication(
    string CaseId,
    string BorrowerName,
    string? CoBorrowerName,
    decimal RequestedLoanAmount,
    decimal? PurchasePrice,
    decimal? MonthlyIncome,
    decimal? MonthlyDebt,
    string? PropertyAddress,
    string? PropertyCityState,
    string? PropertyType,
    string? Product,
    string? Purpose);
