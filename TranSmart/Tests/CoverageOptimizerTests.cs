using System.IO;
using System.Text.Json;
using TranSmart.Models;
using TranSmart.Services;
using Xunit;

namespace TranSmart.Tests;

public sealed class CoverageOptimizerTests
{
    [Fact]
    public void Optimize_UsesShortestWeightedPathAndCoversExactRadiusBoundary()
    {
        var graph = CreateGraph(
            [1, 2, 3],
            [(1, 2, 2.0), (2, 3, 2.0), (1, 3, 9.0)]);

        var result = CoverageOptimizer.Optimize(graph, 4.0);

        Assert.Equal([1L], result.SelectedNodeIds);
        Assert.Equal([1L, 2L, 3L], result.CoveredNodeIds);
        Assert.Empty(result.UncoveredNodeIds);
    }

    [Fact]
    public void Optimize_RespectsRadiusBoundary()
    {
        var graph = CreateGraph([1, 2], [(1, 2, 50.0)]);

        var atBoundary = CoverageOptimizer.Optimize(graph, 50.0);
        var belowBoundary = CoverageOptimizer.Optimize(graph, 49.0);

        Assert.Equal([1L], atBoundary.SelectedNodeIds);
        Assert.Equal([1L, 2L], belowBoundary.SelectedNodeIds);
    }

    [Fact]
    public void Optimize_TieBreaksBySmallestIdAndCoversDisconnectedAndIsolatedVertices()
    {
        var graph = CreateGraph(
            [20, 21, 10, 11, 30],
            [(20, 21, 1.0), (10, 11, 1.0)]);

        var result = CoverageOptimizer.Optimize(graph, 1.0);

        Assert.Equal([10L, 20L, 30L], result.SelectedNodeIds);
        Assert.Equal([10L, 11L, 20L, 21L, 30L], result.CoveredNodeIds);
        Assert.Empty(result.UncoveredNodeIds);
        Assert.Equal(5, result.TotalNodeCount);
        Assert.Equal(3, result.SelectedNodeCount);
        Assert.Equal(5, result.CoveredNodeCount);
        Assert.Equal(100.0, result.CoveragePercentage);
        Assert.Equal(40.0, result.NodeReductionPercentage);
        Assert.True(result.ElapsedMilliseconds >= 0);
    }

    [Fact]
    public void Optimize_GreedyChoiceCanUseMoreSitesThanAnAvailableCover()
    {
        var graph = CreateGraph(
            [1, 2, 3, 10, 11, 12, 13, 20, 21, 22, 23],
            [
                (1, 2, 1.0), (1, 10, 1.0), (1, 11, 1.0), (1, 20, 1.0), (1, 21, 1.0), (1, 22, 1.0),
                (2, 10, 1.0), (2, 11, 1.0), (2, 12, 1.0), (2, 13, 1.0),
                (3, 20, 1.0), (3, 21, 1.0), (3, 22, 1.0), (3, 23, 1.0)
            ]);

        var result = CoverageOptimizer.Optimize(graph, 1.0);
        var knownTwoSiteCover = new HashSet<long> { 2, 3 };
        var knownCoverCount = graph.Nodes.Count(node =>
            knownTwoSiteCover.Contains(node.Id) ||
            node.Edges.Any(edge => knownTwoSiteCover.Contains(graph.Nodes[edge.TargetIndex].Id)));

        Assert.Equal(graph.Nodes.Count, knownCoverCount);
        Assert.Equal([1L, 2L, 3L], result.SelectedNodeIds);
        Assert.Equal(3, result.SelectedNodeCount);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Optimize_RejectsInvalidRadius(double radiusMeters)
    {
        var graph = CreateGraph([1], []);

        Assert.Throws<ArgumentOutOfRangeException>(() => CoverageOptimizer.Optimize(graph, radiusMeters));
    }

    [Fact]
    public void Optimize_SuppliedGraphProducesCompleteCoverageAt500Meters()
    {
        var graph = GraphLoader.Load(Path.Combine(AppContext.BaseDirectory, "amb_graph.json"));

        var result = CoverageOptimizer.Optimize(graph, 500.0);

        Assert.Equal(graph.Nodes.Count, result.CoveredNodeCount);
        Assert.Empty(result.UncoveredNodeIds);
        Assert.Equal(graph.Nodes.Count, result.TotalNodeCount);
        Assert.Equal(500.0, result.RadiusMeters);
    }

    private static Graph CreateGraph(long[] nodeIds, (long Source, long Target, double Length)[] edges)
    {
        var json = JsonSerializer.Serialize(new
        {
            nodes = nodeIds.Select(id => new { id, lat = 11.0, lon = -74.8 }),
            edges = edges.Select(edge => new { source = edge.Source, target = edge.Target, length = edge.Length })
        });
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