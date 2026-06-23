using Cohere.LoanProcessing.Shared.Contracts.Api.Cases;

namespace Cohere.LoanProcessing.WebApp.State;

public sealed class RecentExecutionsListState
{
    public const int DefaultVisibleCount = 6;

    private IReadOnlyList<CaseSummaryResponse> _cases = [];
    private bool _showAll;

    public event Action? OnChange;

    public IReadOnlyList<CaseSummaryResponse> AllCases => _cases;

    public int TotalCount => _cases.Count;

    public bool CanExpand => _cases.Count > DefaultVisibleCount;

    public bool IsExpanded => _showAll;

    public int VisibleCount => CanExpand && !_showAll
        ? DefaultVisibleCount
        : _cases.Count;

    public int HiddenCount => CanExpand && !_showAll
        ? _cases.Count - DefaultVisibleCount
        : 0;

    public IReadOnlyList<CaseSummaryResponse> VisibleCases =>
        CanExpand && !_showAll
            ? _cases.Take(DefaultVisibleCount).ToList()
            : _cases;

    public void SetCases(IReadOnlyList<CaseSummaryResponse> cases)
    {
        _cases = cases
            .OrderByDescending(c => c.UpdatedAt)
            .ToList();
        _showAll = false;
        NotifyStateChanged();
    }

    public void ShowMore()
    {
        if (!CanExpand)
        {
            return;
        }

        _showAll = true;
        NotifyStateChanged();
    }

    public void ShowLess()
    {
        if (!_showAll)
        {
            return;
        }

        _showAll = false;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
