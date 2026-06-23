using Cohere.LoanProcessing.WebApp.Configuration;
using Cohere.LoanProcessing.WebApp.Services;
using Microsoft.Extensions.Options;

namespace Cohere.LoanProcessing.WebApp.Tests;

public sealed class DatasetSeedCatalogServiceTests
{
    [Fact]
    public void GetAllCases_LoadsCasesFromDatasetSeedFolder()
    {
        var service = TestSupport.CreateCatalog();

        var cases = service.GetAllCases();

        Assert.Equal(20, cases.Count);
        Assert.Contains(cases, seedCase => seedCase.CaseId == "APP-001");
        Assert.Contains(cases, seedCase => seedCase.CaseId == "APP-020");
    }

    [Fact]
    public void TryGetCase_ParsesLoanApplicationFields()
    {
        var service = TestSupport.CreateCatalog();

        var app001 = service.TryGetCase("APP-001");
        var app002 = service.TryGetCase("APP-002");

        Assert.NotNull(app001);
        Assert.Equal("Olivia Bennett", app001!.BorrowerName);
        Assert.Equal(390_000m, app001.RequestedLoanAmount);
        Assert.Equal("approve", app001.ExpectedOutcome);

        Assert.NotNull(app002);
        Assert.Equal("Ethan Carter", app002!.BorrowerName);
        Assert.Equal(457_000m, app002.RequestedLoanAmount);
    }

    [Fact]
    public void LoanApplicationTextParser_ReadsStructuredFields()
    {
        string content = """
            Application ID   : APP-002
            Full Name      : Ethan Carter
            Requested Amount : $457,000.00
            Monthly Income : $12,833.00
            """;

        var parsed = LoanApplicationTextParser.Parse(content, "APP-002");

        Assert.Equal("APP-002", parsed.CaseId);
        Assert.Equal("Ethan Carter", parsed.BorrowerName);
        Assert.Equal(457_000m, parsed.RequestedLoanAmount);
        Assert.Equal(12_833m, parsed.MonthlyIncome);
    }
}
