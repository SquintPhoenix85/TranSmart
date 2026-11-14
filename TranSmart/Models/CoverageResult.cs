using System.Collections.ObjectModel;

namespace TranSmart.Models;

public sealed class CoverageResult
{
    private readonly ReadOnlyCollection<long> _selectedNodeIds;
    private readonly ReadOnlyCollection<long> _coveredNodeIds;
    private readonly ReadOnlyCollection<long> _uncoveredNodeIds;
    private readonly ReadOnlyCollection<CoverageStep> _steps;

    internal CoverageResult(
        IEnumerable<long> selectedNodeIds,
        IEnumerable<long> coveredNodeIds,
        IEnumerable<long> uncoveredNodeIds,
        IEnumerable<CoverageStep> steps,
        int totalNodeCount,
        double radiusMeters,
        double elapsedMilliseconds)
    {
        _selectedNodeIds = Array.AsReadOnly(selectedNodeIds.ToArray());
        _coveredNodeIds = Array.AsReadOnly(coveredNodeIds.ToArray());
        _uncoveredNodeIds = Array.AsReadOnly(uncoveredNodeIds.ToArray());
        _steps = Array.AsReadOnly(steps.ToArray());
        TotalNodeCount = totalNodeCount;
        RadiusMeters = radiusMeters;
        ElapsedMilliseconds = elapsedMilliseconds;
    }

    public IReadOnlyList<long> SelectedNodeIds => _selectedNodeIds;

    public IReadOnlyList<long> CoveredNodeIds => _coveredNodeIds;

    public IReadOnlyList<long> UncoveredNodeIds => _uncoveredNodeIds;

    public IReadOnlyList<CoverageStep> Steps => _steps;

    public int TotalNodeCount { get; }

    public int SelectedNodeCount => SelectedNodeIds.Count;

    public int CoveredNodeCount => CoveredNodeIds.Count;

    public bool IsFeasible => UncoveredNodeIds.Count == 0;

    public double RadiusMeters { get; }

    public double ElapsedMilliseconds { get; }

    public double CoveragePercentage => TotalNodeCount == 0 ? 0 : (double)CoveredNodeCount / TotalNodeCount * 100;

    public double NodeReductionPercentage => TotalNodeCount == 0
        ? 0
        : (double)(TotalNodeCount - SelectedNodeCount) / TotalNodeCount * 100;
}