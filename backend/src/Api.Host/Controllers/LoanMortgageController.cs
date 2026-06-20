using CohereLoanAndMortgage.Api.Host.Contracts;
using CohereLoanAndMortgage.Api.Host.Services;
using Microsoft.AspNetCore.Mvc;

namespace CohereLoanAndMortgage.Api.Host.Controllers;

[ApiController]
[Route("api/loan-mortgage")]
public sealed class LoanMortgageController : ControllerBase
{
    private readonly LoanWorkflowService _workflowService;
    private readonly BasicLoanWorkflowService _basicWorkflowService;
    private readonly ILogger<LoanMortgageController> _logger;

    public LoanMortgageController(
        LoanWorkflowService workflowService,
        BasicLoanWorkflowService basicWorkflowService,
        ILogger<LoanMortgageController> logger)
    {
        _workflowService = workflowService;
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

    [HttpPost("applications/{caseId}/workflow/start")]
    public async Task<ActionResult<LoanCaseResponse>> StartWorkflowAsync(
        string caseId,
        CancellationToken cancellationToken)
    {
        string executionId = Guid.NewGuid().ToString("N");

        try
        {
            _logger.LogInformation(
                "Starting workflow for case {CaseId} with execution {ExecutionId}.",
                caseId,
                executionId);

            LoanCaseResponse response = await _workflowService.StartWorkflowAsync(
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
                Title = "Workflow cannot be started.",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to start loan workflow for case {CaseId} with execution {ExecutionId}.",
                caseId,
                executionId);

            return Problem(
                detail: ex.Message,
                title: "Loan workflow failed to start.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("executions/{executionId}")]
    public ActionResult<LoanCaseResponse> GetExecutionAsync(string executionId)
    {
        try
        {
            return Ok(_workflowService.GetExecution(executionId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetailsResponse
            {
                Title = "Workflow execution not found.",
                Detail = ex.Message
            });
        }
    }

    [HttpGet("executions/{executionId}/progress")]
    public ActionResult<LoanProgressResponse> GetProgressAsync(string executionId)
    {
        try
        {
            return Ok(_workflowService.GetProgress(executionId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetailsResponse
            {
                Title = "Workflow execution not found.",
                Detail = ex.Message
            });
        }
    }

    [HttpPost("executions/{executionId}/decisions")]
    public async Task<ActionResult<LoanCaseResponse>> SubmitDecisionAsync(
        string executionId,
        [FromBody] HumanDecisionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            LoanCaseResponse response = await _workflowService.SubmitDecisionAsync(
                executionId,
                request,
                cancellationToken);

            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetailsResponse
            {
                Title = "Workflow execution not found.",
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
            _logger.LogError(ex, "Failed to process human decision for execution {ExecutionId}.", executionId);
            return Problem(
                detail: ex.Message,
                title: "Loan workflow failed after human decision.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
