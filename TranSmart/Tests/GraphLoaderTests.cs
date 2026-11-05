using System.IO;
using System.Text.Json;
using TranSmart.Models;
using TranSmart.Services;
using Xunit;

namespace TranSmart.Tests;

public sealed class GraphLoaderTests
{
    [Fact]
    public void Load_Preserves64BitIdsAndIsolatedNodes()
    {
        var graph = Load("""
                         {
                           "nodes": [
                             { "id": 13423874722, "lat": 11.0, "lon": -74.8 },
                             { "id": 2, "lat": 10.9, "lon": -74.7 }
                           ],
                           "edges": []
                         }
                         """);

        Assert.Equal(13423874722L, graph.Nodes[0].Id);
        Assert.Equal(0, graph.EdgeCount);
        Assert.Empty(graph.Nodes[0].Edges);
        Assert.Empty(graph.Nodes[1].Edges);
        Assert.True(graph.TryGetNodeIndex(13423874722L, out var index));
        Assert.Equal(0, index);
    }

    [Fact]
    public void Load_NormalizesReciprocalAndParallelEdgesToShortestUndirectedEdge()
    {
        var graph = Load("""
                         {
                           "nodes": [
                             { "id": 1, "lat": 11.0, "lon": -74.8 },
                             { "id": 2, "lat": 11.1, "lon": -74.7 },
                             { "id": 3, "lat": 11.2, "lon": -74.6 }
                           ],
                           "edges": [
                             { "source": 1, "target": 2, "length": 7.0 },
                             { "source": 2, "target": 1, "length": 8.0 },
                             { "source": 1, "target": 2, "length": 4.0 }
                           ]
                         }
                         """);

        Assert.Equal(1, graph.EdgeCount);
        Assert.Single(graph.Nodes[0].Edges);
        Assert.Single(graph.Nodes[1].Edges);
        Assert.Equal(4.0, graph.Nodes[0].Edges[0].LengthMeters);
        Assert.Equal(4.0, graph.Nodes[1].Edges[0].LengthMeters);
        Assert.Equal(1, graph.Nodes[0].Edges[0].TargetIndex);
        Assert.Equal(0, graph.Nodes[1].Edges[0].TargetIndex);
        Assert.Empty(graph.Nodes[2].Edges);
    }

    [Fact]
    public void Load_RejectsDuplicateNodeIds()
    {
        var exception = Assert.Throws<InvalidDataException>(() => Load("""
                                                                       {
                                                                         "nodes": [
                                                                           { "id": 1, "lat": 11.0, "lon": -74.8 },
                                                                           { "id": 1, "lat": 11.1, "lon": -74.7 }
                                                                         ],
                                                                         "edges": []
                                                                       }
                                                                       """));

        Assert.Contains("duplicate node ID 1", exception.Message);
    }

    [Fact]
    public void Load_RejectsUnknownEdgeEndpoint()
    {
        var exception = Assert.Throws<InvalidDataException>(() => Load("""
                                                                       {
                                                                         "nodes": [ { "id": 1, "lat": 11.0, "lon": -74.8 } ],
                                                                         "edges": [ { "source": 1, "target": 2, "length": 5.0 } ]
                                                                       }
                                                                       """));

        Assert.Contains("unknown target node ID 2", exception.Message);
    }

    [Theory]
    [InlineData("-1.0")]
    [InlineData("1e309")]
    public void Load_RejectsInvalidEdgeLengths(string length)
    {
        var json = $$"""
                     {
                       "nodes": [
                         { "id": 1, "lat": 11.0, "lon": -74.8 },
                         { "id": 2, "lat": 11.1, "lon": -74.7 }
                       ],
                       "edges": [ { "source": 1, "target": 2, "length": {{length}} } ]
                     }
                     """;

        Assert.Throws<InvalidDataException>(() => Load(json));
    }

    [Fact]
    public void Load_RejectsNonFiniteCoordinates()
    {
        Assert.Throws<InvalidDataException>(() => Load("""
                                                       {
                                                         "nodes": [ { "id": 1, "lat": 1e309, "lon": -74.8 } ],
                                                         "edges": []
                                                       }
                                                       """));
    }

    [Fact]
    public void Load_SuppliedGraphHasExpectedNodeAndRecordCounts()
    {
        var graphPath = Path.Combine(AppContext.BaseDirectory, "amb_graph.json");
        var graph = GraphLoader.Load(graphPath);
        using var document = JsonDocument.Parse(File.ReadAllText(graphPath));

        Assert.Equal(852, graph.Nodes.Count);
        Assert.Equal(1920, document.RootElement.GetProperty("edges").GetArrayLength());
    }

    private static Graph Load(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"transmart-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, json);
            return GraphLoader.Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}