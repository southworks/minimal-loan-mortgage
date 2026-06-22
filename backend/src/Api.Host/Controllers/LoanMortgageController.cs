using CohereLoanAndMortgage.Api.Host.Contracts;
using CohereLoanAndMortgage.Api.Host.Services;
using Microsoft.AspNetCore.Mvc;

namespace CohereLoanAndMortgage.Api.Host.Controllers;

[ApiController]
[Route("api/loan-mortgage")]
public sealed class LoanMortgageController : ControllerBase
{
    private readonly BasicLoanWorkflowService _basicWorkflowService;
    private readonly ILogger<LoanMortgageController> _logger;

    public LoanMortgageController(
        BasicLoanWorkflowService basicWorkflowService,
        ILogger<LoanMortgageController> logger)
    {
        _basicWorkflowService = basicWorkflowService;
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
    public async Task<ActionResult<BasicWorkflowStatusResponse>> ResumeBasicWorkflowAsync(
        string caseId,
        string executionId,
        [FromBody] BasicWorkflowApprovalRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            BasicWorkflowStatusResponse response = await _basicWorkflowService.ResumeBasicWorkflowAsync(
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
