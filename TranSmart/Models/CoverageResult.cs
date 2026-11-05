using System.Collections.ObjectModel;

namespace TranSmart.Models;

public sealed class CoverageResult
{
    private readonly ReadOnlyCollection<long> _selectedNodeIds;
    private readonly ReadOnlyCollection<long> _coveredNodeIds;
    private readonly ReadOnlyCollection<long> _uncoveredNodeIds;

    internal CoverageResult(
        IEnumerable<long> selectedNodeIds,
        IEnumerable<long> coveredNodeIds,
        IEnumerable<long> uncoveredNodeIds,
        int totalNodeCount,
        double radiusMeters,
        double elapsedMilliseconds)
    {
        _selectedNodeIds = Array.AsReadOnly(selectedNodeIds.ToArray());
        _coveredNodeIds = Array.AsReadOnly(coveredNodeIds.ToArray());
        _uncoveredNodeIds = Array.AsReadOnly(uncoveredNodeIds.ToArray());
        TotalNodeCount = totalNodeCount;
        RadiusMeters = radiusMeters;
        ElapsedMilliseconds = elapsedMilliseconds;
    }

    public IReadOnlyList<long> SelectedNodeIds => _selectedNodeIds;

    public IReadOnlyList<long> CoveredNodeIds => _coveredNodeIds;

    public IReadOnlyList<long> UncoveredNodeIds => _uncoveredNodeIds;

    public int TotalNodeCount { get; }

    public int SelectedNodeCount => SelectedNodeIds.Count;

    public int CoveredNodeCount => CoveredNodeIds.Count;

    public double RadiusMeters { get; }

    public double ElapsedMilliseconds { get; }

    public double CoveragePercentage => TotalNodeCount == 0 ? 0 : (double)CoveredNodeCount / TotalNodeCount * 100;

    public double NodeReductionPercentage => TotalNodeCount == 0
        ? 0
        : (double)(TotalNodeCount - SelectedNodeCount) / TotalNodeCount * 100;
}