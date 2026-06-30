using CohereLoanAndMortgage.Api.Host.Contracts;
using CohereLoanAndMortgage.Api.Host.Services;
using LoanWorkflow.Mcp.Adapters;
using Microsoft.AspNetCore.Mvc;

namespace CohereLoanAndMortgage.Api.Host.Controllers;

[ApiController]
[Route("api/loan-mortgage")]
public sealed class LoanMortgageController : ControllerBase
{
    private readonly BasicLoanWorkflowService _basicWorkflowService;
    private readonly LocalCaseDocumentService _documentStorageService;
    private readonly ILogger<LoanMortgageController> _logger;

    public LoanMortgageController(
        BasicLoanWorkflowService basicWorkflowService,
        LocalCaseDocumentService documentStorageService,
        ILogger<LoanMortgageController> logger)
    {
        _basicWorkflowService = basicWorkflowService;
        _documentStorageService = documentStorageService;
        _logger = logger;
    }

    [HttpPost("applications/{caseId}/workflow/basic/start")]
    public async Task<ActionResult<BasicWorkflowStatusResponse>> StartBasicWorkflowAsync(
        string caseId,
        CancellationToken cancellationToken)
    {
        string executionId = Guid.NewGuid().ToString("N");

        try
        {
            _logger.LogInformation(
                "Starting basic workflow for case {CaseId} with execution {ExecutionId}.",
                caseId,
                executionId);

            BasicWorkflowStatusResponse response = await _basicWorkflowService.StartBasicWorkflowAsync(
                caseId,
                executionId,
                cancellationToken);

            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetailsResponse
            {
                Title = "Loan case not found.",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetailsResponse
            {
                Title = "Basic workflow cannot be started.",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to start basic loan workflow for case {CaseId} with execution {ExecutionId}.",
                caseId,
                executionId);

            return Problem(
                detail: ex.Message,
                title: "Basic loan workflow failed to start.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("applications/{caseId}/documents")]
    public async Task<ActionResult<CaseDocumentsResponse>> GetCaseDocumentsAsync(
        string caseId,
        CancellationToken cancellationToken)
    {
        try
        {
            string normalizedCaseId = CasePathResolver.NormalizeCaseId(caseId);
            IReadOnlyList<CaseDocumentInfo> documents = await _documentStorageService.ListCaseDocumentsAsync(
                normalizedCaseId,
                cancellationToken);

            if (documents.Count == 0)
            {
                return NotFound(new ProblemDetailsResponse
                {
                    Title = "Loan case not found.",
                    Detail = $"Case '{normalizedCaseId}' was not found in dataset assets or has no documents under '{_documentStorageService.GetCaseIngestRelativePath(normalizedCaseId)}'."
                });
            }

            return Ok(new CaseDocumentsResponse
            {
                CaseId = normalizedCaseId,
                Documents = documents
                    .Select(document => new CaseDocumentResponse
                    {
                        FileName = document.FileName,
                        ContentType = document.ContentType,
                        SourcePath = document.SourcePath,
                        Reference = document.Reference,
                        LastModifiedUtc = document.LastModifiedUtc
                    })
                    .ToArray()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to list documents for case {CaseId}.",
                caseId);

            return Problem(
                detail: ex.Message,
                title: "Failed to list case documents.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("applications/{caseId}/documents/content")]
    public async Task<IActionResult> GetCaseDocumentContentAsync(
        string caseId,
        [FromQuery] string sourcePath,
        CancellationToken cancellationToken)
    {
        try
        {
            LoadedCaseDocument document = await _documentStorageService.GetCaseDocumentAsync(
                caseId,
                sourcePath,
                cancellationToken);

            return File(
                document.Content.ToArray(),
                document.ContentType,
                document.FileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetailsResponse
            {
                Title = "Loan case document not found.",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetailsResponse
            {
                Title = "Loan case document request is invalid.",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load document {SourcePath} for case {CaseId}.",
                sourcePath,
                caseId);

            return Problem(
                detail: ex.Message,
                title: "Failed to load case document.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("executions/{executionId}/basic/status")]
    public ActionResult<BasicWorkflowStatusResponse> GetStatusBasicWorkflowAsync(string executionId)
    {
        try
        {
            return Ok(_basicWorkflowService.GetBasicWorkflowStatus(executionId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetailsResponse
            {
                Title = "Basic workflow execution not found.",
                Detail = ex.Message
            });
        }
    }

    [HttpPost("applications/{caseId}/workflow/basic/executions/{executionId}/resume")]
    public ActionResult<BasicWorkflowStatusResponse> ResumeBasicWorkflowAsync(
        string caseId,
        string executionId,
        [FromBody] BasicWorkflowApprovalRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            BasicWorkflowStatusResponse response = _basicWorkflowService.ResumeBasicWorkflowAsync(
                caseId,
                executionId,
                request.Approved,
                request.ReviewerComment,
                cancellationToken);

            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetailsResponse
            {
                Title = "Basic workflow execution not found.",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetailsResponse
            {
                Title = "Basic workflow cannot be resumed.",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to resume basic workflow for case {CaseId} with execution {ExecutionId}.",
                caseId,
                executionId);

            return Problem(
                detail: ex.Message,
                title: "Basic workflow failed to resume.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
