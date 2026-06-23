using Cohere.LoanProcessing.Shared.Contracts.Api.Cases;

namespace Cohere.LoanProcessing.WebApp.State;

public sealed class ScenarioPickerState
{
    private IReadOnlyList<ScenarioSummaryResponse> _scenarios = [];
    private string _searchQuery = string.Empty;
    private ScenarioOutcomeFilter _outcomeFilter = ScenarioOutcomeFilter.All;
    private string? _selectedScenarioId;

    public event Action? OnChange;

    public IReadOnlyList<ScenarioSummaryResponse> Scenarios => _scenarios;

    public string SearchQuery => _searchQuery;

    public ScenarioOutcomeFilter OutcomeFilter => _outcomeFilter;

    public string? SelectedScenarioId => _selectedScenarioId;

    public IReadOnlyList<ScenarioSummaryResponse> FilteredScenarios =>
        ScenarioPickerFilter.Apply(_scenarios, _searchQuery, _outcomeFilter);

    public ScenarioSummaryResponse? SelectedScenario =>
        _selectedScenarioId is null
            ? null
            : _scenarios.FirstOrDefault(s =>
                string.Equals(s.ScenarioId, _selectedScenarioId, StringComparison.OrdinalIgnoreCase));

    public bool HasSelection => SelectedScenario is not null;

    public bool HasResults => FilteredScenarios.Count > 0;

    public void SetScenarios(IReadOnlyList<ScenarioSummaryResponse> scenarios)
    {
        _scenarios = scenarios;
        EnsureSelectionValid();
        NotifyStateChanged();
    }

    public void SetSearch(string searchQuery)
    {
        _searchQuery = searchQuery ?? string.Empty;
        SelectFirstFilteredIfNeeded();
        NotifyStateChanged();
    }

    public void SetOutcomeFilter(ScenarioOutcomeFilter outcomeFilter)
    {
        _outcomeFilter = outcomeFilter;
        SelectFirstFilteredIfNeeded();
        NotifyStateChanged();
    }

    public void SelectScenario(string scenarioId)
    {
        _selectedScenarioId = scenarioId;
        NotifyStateChanged();
    }

    public void ClearFilters()
    {
        _searchQuery = string.Empty;
        _outcomeFilter = ScenarioOutcomeFilter.All;
        SelectFirstFilteredIfNeeded();
        NotifyStateChanged();
    }

    private void EnsureSelectionValid()
    {
        if (_selectedScenarioId is not null
            && _scenarios.Any(s => string.Equals(s.ScenarioId, _selectedScenarioId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        SelectFirstFilteredIfNeeded();
    }

    private void SelectFirstFilteredIfNeeded()
    {
        var filtered = FilteredScenarios;
        if (filtered.Count == 0)
        {
            _selectedScenarioId = null;
            return;
        }

        if (_selectedScenarioId is not null
            && filtered.Any(s => string.Equals(s.ScenarioId, _selectedScenarioId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _selectedScenarioId = filtered[0].ScenarioId;
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
