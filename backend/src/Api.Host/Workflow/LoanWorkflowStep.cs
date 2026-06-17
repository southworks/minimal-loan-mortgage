namespace CohereLoanAndMortgage.Api.Host.Workflow;

public enum LoanWorkflowStep
{
    Submitted,
    DocumentProcessing,
    Underwriting,
    WaitingForHumanApproval,
    ResponsibleAi,
    LoanSetup,
    Completed,
    Rejected,
    Failed
}

public enum LoanCaseStatus
{
    Pending,
    Running,
    WaitingForHuman,
    Completed,
    Rejected,
    Failed
}

public enum ApprovalType
{
    Underwriting
}
