using System.Globalization;
using Cohere.LoanProcessing.Shared.Contracts.Api.Cases;

namespace Cohere.LoanProcessing.WebApp.Models;

public static class CaseDisplayFormatting
{
    public static string FormatLoanTitle(ApplicantProfileDto applicant) =>
        FormatLoanTitle(applicant.FullName, applicant.RequestedLoanAmount);

    public static string FormatLoanTitle(string borrowerName, decimal requestedLoanAmount) =>
        $"{borrowerName} loan for {FormatUsdAmount(requestedLoanAmount)} USD";

    private static string FormatUsdAmount(decimal amount) =>
        decimal.Truncate(amount).ToString("0", CultureInfo.InvariantCulture);
}
