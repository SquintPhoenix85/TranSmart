using System.Collections.ObjectModel;

namespace TranSmart.Models;

public sealed class Graph
{
    private readonly ReadOnlyCollection<GraphNode> _nodes;
    private readonly ReadOnlyCollection<long> _candidateSiteIds;
    private readonly Dictionary<long, int> _nodeIndices;

    internal Graph(IReadOnlyList<GraphNode> nodes, IReadOnlyList<AtmLocation> atms, int edgeCount)
    {
        _nodes = Array.AsReadOnly(nodes.ToArray());
        _candidateSiteIds = Array.AsReadOnly(_nodes.Where(node => node.IsCandidateSite).Select(node => node.Id).ToArray());
        _nodeIndices = new Dictionary<long, int>(_nodes.Count);

        for (var index = 0; index < _nodes.Count; index++)
        {
            _nodeIndices.Add(_nodes[index].Id, index);
        }

        EdgeCount = edgeCount;
        Atms = Array.AsReadOnly(atms.ToArray());
    }

    public IReadOnlyList<GraphNode> Nodes => _nodes;

    public IReadOnlyList<long> CandidateSiteIds => _candidateSiteIds;

    public int CandidateSiteCount => _candidateSiteIds.Count;

    public int EdgeCount { get; }

    public IReadOnlyList<AtmLocation> Atms { get; }

    public bool TryGetNodeIndex(long nodeId, out int index) => _nodeIndices.TryGetValue(nodeId, out index);
}

public sealed record AtmLocation(string Id, string Name, double Latitude, double Longitude);

public sealed class GraphNode
{
    private readonly ReadOnlyCollection<GraphEdge> _edges;

    internal GraphNode(long id, double latitude, double longitude, bool isCandidateSite, IEnumerable<GraphEdge> edges)
    {
        Id = id;
        Latitude = latitude;
        Longitude = longitude;
        IsCandidateSite = isCandidateSite;
        _edges = Array.AsReadOnly(edges.ToArray());
    }

    public long Id { get; }

    public double Latitude { get; }

    public double Longitude { get; }

    public bool IsCandidateSite { get; }

    public IReadOnlyList<GraphEdge> Edges => _edges;
}

public readonly record struct GraphEdge(int TargetIndex, double LengthMeters);
