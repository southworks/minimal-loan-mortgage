using CohereLoanAndMortgage.Api.Host.Contracts;
using CohereLoanAndMortgage.Api.Host.Services;
using Microsoft.AspNetCore.Mvc;

namespace CohereLoanAndMortgage.Api.Host.Controllers;

[ApiController]
[Route("api/loan-mortgage")]
public sealed class LoanMortgageController : ControllerBase
{
    private readonly LoanWorkflowService _workflowService;
    private readonly ILogger<LoanMortgageController> _logger;

    public LoanMortgageController(LoanWorkflowService workflowService, ILogger<LoanMortgageController> logger)
    {
        _workflowService = workflowService;
        _logger = logger;
    }

    [HttpPost("applications")]
    public ActionResult<LoanCaseResponse> CreateApplication([FromBody] CreateLoanApplicationRequest request)
    {
        try
        {
            LoanCaseResponse response = _workflowService.CreateCase(request);
            return CreatedAtAction(nameof(GetApplicationAsync), new { caseId = response.CaseId }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create loan application.");
            return Problem(
                detail: ex.Message,
                title: "Loan application could not be created.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost("applications/{caseId}/documents")]
    [RequestSizeLimit(52_428_800)]
    public async Task<ActionResult<UploadDocumentsResponse>> UploadDocumentsAsync(
        string caseId,
        [FromForm] IFormFileCollection files,
        CancellationToken cancellationToken)
    {
        try
        {
            UploadDocumentsResponse response = await _workflowService.UploadDocumentsAsync(
                caseId,
                files.ToArray(),
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
                Title = "Documents cannot be uploaded.",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload documents for case {CaseId}.", caseId);
            return Problem(
                detail: ex.Message,
                title: "Document upload failed.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost("applications/{caseId}/workflow/start")]
    public async Task<ActionResult<LoanCaseResponse>> StartWorkflowAsync(
        string caseId,
        CancellationToken cancellationToken)
    {
        try
        {
            LoanCaseResponse response = await _workflowService.StartWorkflowAsync(caseId, cancellationToken);
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
                Title = "Workflow cannot be started.",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start loan workflow for case {CaseId}.", caseId);
            return Problem(
                detail: ex.Message,
                title: "Loan workflow failed to start.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("applications/{caseId}")]
    public ActionResult<LoanCaseResponse> GetApplicationAsync(string caseId)
    {
        try
        {
            return Ok(_workflowService.GetCase(caseId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetailsResponse
            {
                Title = "Loan case not found.",
                Detail = ex.Message
            });
        }
    }

    [HttpGet("applications/{caseId}/progress")]
    public ActionResult<LoanProgressResponse> GetProgressAsync(string caseId)
    {
        try
        {
            return Ok(_workflowService.GetProgress(caseId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetailsResponse
            {
                Title = "Loan case not found.",
                Detail = ex.Message
            });
        }
    }

    [HttpPost("applications/{caseId}/decisions")]
    public async Task<ActionResult<LoanCaseResponse>> SubmitDecisionAsync(
        string caseId,
        [FromBody] HumanDecisionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            LoanCaseResponse response = await _workflowService.SubmitDecisionAsync(
                caseId,
                request,
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
                Title = "Decision cannot be submitted.",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process human decision for case {CaseId}.", caseId);
            return Problem(
                detail: ex.Message,
                title: "Loan workflow failed after human decision.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
