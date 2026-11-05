using System.Collections.ObjectModel;

namespace TranSmart.Models;

public sealed class Graph
{
    private readonly ReadOnlyCollection<GraphNode> _nodes;
    private readonly Dictionary<long, int> _nodeIndices;

    internal Graph(IReadOnlyList<GraphNode> nodes, int edgeCount)
    {
        _nodes = Array.AsReadOnly(nodes.ToArray());
        _nodeIndices = new Dictionary<long, int>(_nodes.Count);

        for (var index = 0; index < _nodes.Count; index++)
        {
            _nodeIndices.Add(_nodes[index].Id, index);
        }

        EdgeCount = edgeCount;
    }

    public IReadOnlyList<GraphNode> Nodes => _nodes;

    public int EdgeCount { get; }

    public bool TryGetNodeIndex(long nodeId, out int index) => _nodeIndices.TryGetValue(nodeId, out index);
}

public sealed class GraphNode
{
    private readonly ReadOnlyCollection<GraphEdge> _edges;

    internal GraphNode(long id, double latitude, double longitude, IEnumerable<GraphEdge> edges)
    {
        Id = id;
        Latitude = latitude;
        Longitude = longitude;
        _edges = Array.AsReadOnly(edges.ToArray());
    }

    public long Id { get; }

    public double Latitude { get; }

    public double Longitude { get; }

    public IReadOnlyList<GraphEdge> Edges => _edges;
}

public readonly record struct GraphEdge(int TargetIndex, double LengthMeters);