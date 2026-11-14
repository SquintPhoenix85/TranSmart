using System.IO;
using System.Text.Json;
using TranSmart.Models;
using TranSmart.Services;
using Xunit;

namespace TranSmart.Tests;

public sealed class CoverageOptimizerTests
{
    [Fact]
    public void Optimize_DefaultUsesCandidatesButKeepsNonCandidatesAsDemand()
    {
        var graph = CreateGraph([1, 2, 3], [(1, 2, 1.0)], [1]);
        var result = CoverageOptimizer.Optimize(graph, 1);

        Assert.Equal([1L], result.SelectedNodeIds);
        Assert.Equal([1L, 2L], result.CoveredNodeIds);
        Assert.Equal([3L], result.UncoveredNodeIds);
        Assert.False(result.IsFeasible);
    }
    [Fact]
    public void Optimize_RestrictsCandidatesButCoversEveryNode()
    {
        var graph = CreateGraph([1, 2, 3], [(1, 2, 1.0), (2, 3, 1.0)]);
        var result = CoverageOptimizer.Optimize(graph, 1, [2, 2]);

        Assert.True(result.IsFeasible);
        Assert.Equal([2L], result.SelectedNodeIds);
        Assert.Equal([1L, 2L, 3L], result.CoveredNodeIds);
    }

    [Fact]
    public void Optimize_ReportsUnreachableDemandAndEmptyCandidates()
    {
        var graph = CreateGraph([1, 2, 3], [(1, 2, 0.0)]);
        var partial = CoverageOptimizer.Optimize(graph, 0, [1]);
        var empty = CoverageOptimizer.Optimize(graph, 0, []);

        Assert.False(partial.IsFeasible);
        Assert.Equal([1L, 2L], partial.CoveredNodeIds);
        Assert.Equal([3L], partial.UncoveredNodeIds);
        Assert.False(empty.IsFeasible);
        Assert.Equal([1L, 2L, 3L], empty.UncoveredNodeIds);
        Assert.Empty(empty.Steps);
        Assert.True(CoverageOptimizer.Optimize(CreateGraph([], []), 0, []).IsFeasible);
    }

    [Fact]
    public void Optimize_RejectsUnknownCandidates()
    {
        var graph = CreateGraph([1], []);
        Assert.Throws<ArgumentException>(() => CoverageOptimizer.Optimize(graph, 0, [2]));
        Assert.Throws<ArgumentNullException>(() => CoverageOptimizer.Optimize(graph, 0, null!));
    }

    [Fact]
    public void Optimize_RejectsIneligibleCandidates()
    {
        var graph = CreateGraph([1, 2], [(1, 2, 1.0)], [1]);

        var exception = Assert.Throws<ArgumentException>(() => CoverageOptimizer.Optimize(graph, 1, [1, 2]));

        Assert.Equal("candidateNodeIds", exception.ParamName);
        Assert.Contains("2", exception.Message);
        Assert.Contains("eligible", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

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
    public void Optimize_RecordsIncrementalFirstCoverageForEachGreedyChoice()
    {
        var graph = CreateGraph(
            [1, 2, 3, 4],
            [(1, 2, 1.0), (2, 3, 1.0), (3, 4, 1.0)]);

        var result = CoverageOptimizer.Optimize(graph, 1.0);

        Assert.Equal([2L, 3L], result.SelectedNodeIds);
        Assert.Collection(
            result.Steps,
            first =>
            {
                Assert.Equal(1, first.StepNumber);
                Assert.Equal(2L, first.SelectedNodeId);
                Assert.Equal(3, first.Gain);
                Assert.Equal([1L, 2L, 3L], first.NewlyCoveredNodeIds);
                Assert.Equal(3, first.CoveredVertexCount);
            },
            second =>
            {
                Assert.Equal(2, second.StepNumber);
                Assert.Equal(3L, second.SelectedNodeId);
                Assert.Equal(1, second.Gain);
                Assert.Equal([4L], second.NewlyCoveredNodeIds);
                Assert.Equal(4, second.CoveredVertexCount);
            });

        Assert.Equal(result.CoveredNodeIds.Order(), result.Steps.SelectMany(step => step.NewlyCoveredNodeIds).Order());
        Assert.Equal(result.CoveredNodeCount, result.Steps[^1].CoveredVertexCount);
    }

    [Fact]
    public void Optimize_EmptyGraphProducesAnEmptyDecisionTrace()
    {
        var result = CoverageOptimizer.Optimize(CreateGraph([], []), 0);

        Assert.Empty(result.Steps);
        Assert.Empty(result.SelectedNodeIds);
        Assert.Empty(result.CoveredNodeIds);
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
    public void Optimize_SuppliedGraphReportsCoverageAt500Meters()
    {
        var graph = GraphLoader.Load(Path.Combine(AppContext.BaseDirectory, "amb_graph.json"));

        var result = CoverageOptimizer.Optimize(graph, 500.0);

        Assert.All(result.SelectedNodeIds, id => Assert.Contains(id, graph.CandidateSiteIds));
        Assert.Equal(graph.Nodes.Select(node => node.Id).Order(), result.CoveredNodeIds.Concat(result.UncoveredNodeIds).Order());
        Assert.Equal(result.UncoveredNodeIds.Count == 0, result.IsFeasible);
        Assert.Equal(graph.Nodes.Count, result.TotalNodeCount);
        Assert.Equal(500.0, result.RadiusMeters);
    }

    private static Graph CreateGraph(long[] nodeIds, (long Source, long Target, double Length)[] edges, long[]? candidates = null)
    {
        var json = JsonSerializer.Serialize(new
        {
            schemaVersion = 2,
            networkType = "walk",
            nodes = nodeIds.Select(id => new { id, lat = 11.0, lon = -74.8, isCandidateSite = candidates is null || candidates.Contains(id) }),
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