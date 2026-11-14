using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TranSmart.Models;

namespace TranSmart.Services;

public static class GraphLoader
{
    public static Graph Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        GraphDocument? document;
        try
        {
            using var stream = File.OpenRead(path);
            document = JsonSerializer.Deserialize<GraphDocument>(stream);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Graph file '{path}' contains invalid JSON or a value outside the supported format: {exception.Message}",
                exception);
        }
        catch (IOException exception)
        {
            throw new IOException($"Could not read graph file '{path}': {exception.Message}", exception);
        }

        if (document is null)
        {
            throw new InvalidDataException($"Graph file '{path}' must contain a JSON object.");
        }

        if (document.SchemaVersion != 2 || document.NetworkType != "walk")
        {
            throw new InvalidDataException($"Graph file '{path}' must have schemaVersion 2 and networkType 'walk'. Regenerate stale graph data.");
        }

        var records = document.Nodes ?? throw new InvalidDataException($"Graph file '{path}' must contain a non-null nodes array.");
        var edgeRecords = document.Edges ?? throw new InvalidDataException($"Graph file '{path}' must contain a non-null edges array.");
        var nodeIndices = new Dictionary<long, int>(records.Count);
        var nodes = new NodeRecord[records.Count];

        for (var index = 0; index < records.Count; index++)
        {
            var node = records[index] ??
                       throw new InvalidDataException($"Graph file '{path}' contains a null node at index {index}.");
            if (!nodeIndices.TryAdd(node.Id, index))
            {
                throw new InvalidDataException($"Graph file '{path}' contains duplicate node ID {node.Id}.");
            }

            if (!double.IsFinite(node.Latitude) || !double.IsFinite(node.Longitude))
            {
                throw new InvalidDataException($"Node {node.Id} must have finite latitude and longitude coordinates.");
            }

            nodes[index] = node;
        }

        var edgeLengths = new Dictionary<(long Source, long Target), double>();
        for (var index = 0; index < edgeRecords.Count; index++)
        {
            var edge = edgeRecords[index] ??
                       throw new InvalidDataException($"Graph file '{path}' contains a null edge at index {index}.");
            if (!nodeIndices.ContainsKey(edge.Source))
            {
                throw new InvalidDataException(
                    $"Edge at index {index} references unknown source node ID {edge.Source}.");
            }

            if (!nodeIndices.ContainsKey(edge.Target))
            {
                throw new InvalidDataException(
                    $"Edge at index {index} references unknown target node ID {edge.Target}.");
            }

            if (!double.IsFinite(edge.Length) || edge.Length < 0)
            {
                throw new InvalidDataException($"Edge at index {index} must have a finite, nonnegative length.");
            }

            (long Source, long Target) key = edge.Source <= edge.Target
                ? (edge.Source, edge.Target)
                : (edge.Target, edge.Source);
            if (!edgeLengths.TryGetValue(key, out var existingLength) || edge.Length < existingLength)
            {
                edgeLengths[key] = edge.Length;
            }
        }

        var adjacency = new List<GraphEdge>[nodes.Length];
        for (var index = 0; index < adjacency.Length; index++)
        {
            adjacency[index] = [];
        }

        foreach (var (endpoints, length) in edgeLengths)
        {
            var sourceIndex = nodeIndices[endpoints.Source];
            var targetIndex = nodeIndices[endpoints.Target];
            adjacency[sourceIndex].Add(new GraphEdge(targetIndex, length));
            if (sourceIndex != targetIndex)
            {
                adjacency[targetIndex].Add(new GraphEdge(sourceIndex, length));
            }
        }

        var graphNodes = new GraphNode[nodes.Length];
        for (var index = 0; index < nodes.Length; index++)
        {
            var node = nodes[index];
            graphNodes[index] = new GraphNode(node.Id, node.Latitude, node.Longitude, node.IsCandidateSite, adjacency[index]);
        }

        return new Graph(graphNodes, edgeLengths.Count);
    }

    private sealed class GraphDocument
    {
        [JsonPropertyName("schemaVersion")] public required int SchemaVersion { get; init; }

        [JsonPropertyName("networkType")] public required string? NetworkType { get; init; }
        [JsonPropertyName("nodes")] public required List<NodeRecord?> Nodes { get; init; }

        [JsonPropertyName("edges")] public required List<EdgeRecord?> Edges { get; init; }
    }

    private sealed class NodeRecord
    {
        [JsonPropertyName("id")] public required long Id { get; init; }

        [JsonPropertyName("lat")] public required double Latitude { get; init; }

        [JsonPropertyName("lon")] public required double Longitude { get; init; }

        [JsonPropertyName("isCandidateSite")] public required bool IsCandidateSite { get; init; }
    }

    private sealed class EdgeRecord
    {
        [JsonPropertyName("source")] public required long Source { get; init; }

        [JsonPropertyName("target")] public required long Target { get; init; }

        [JsonPropertyName("length")] public required double Length { get; init; }
    }
}