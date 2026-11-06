using System.Collections.ObjectModel;

namespace TranSmart.Models;

public sealed class CoverageStep
{
    private readonly ReadOnlyCollection<long> _newlyCoveredNodeIds;

    internal CoverageStep(
        int stepNumber,
        long selectedNodeId,
        IEnumerable<long> newlyCoveredNodeIds,
        int coveredVertexCount)
    {
        StepNumber = stepNumber;
        SelectedNodeId = selectedNodeId;
        _newlyCoveredNodeIds = Array.AsReadOnly(newlyCoveredNodeIds.ToArray());
        CoveredVertexCount = coveredVertexCount;
    }

    public int StepNumber { get; }

    public long SelectedNodeId { get; }

    public IReadOnlyList<long> NewlyCoveredNodeIds => _newlyCoveredNodeIds;

    public int Gain => NewlyCoveredNodeIds.Count;

    public int CoveredVertexCount { get; }
}