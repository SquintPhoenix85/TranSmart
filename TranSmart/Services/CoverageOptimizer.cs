using System.Diagnostics;
using TranSmart.Models;

namespace TranSmart.Services;

public static class CoverageOptimizer
{
    public static CoverageResult Optimize(Graph graph, double radiusMeters)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return Optimize(graph, radiusMeters, graph.CandidateSiteIds);
    }

    public static CoverageResult Optimize(Graph graph, double radiusMeters, IEnumerable<long> candidateNodeIds)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(candidateNodeIds);
        if (!double.IsFinite(radiusMeters) || radiusMeters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusMeters), "Radius must be finite and nonnegative.");
        }

        var stopwatch = Stopwatch.StartNew();
        var nodeCount = graph.Nodes.Count;
        var candidateIndices = new HashSet<int>();
        foreach (var id in candidateNodeIds)
        {
            if (!graph.TryGetNodeIndex(id, out var index))
            {
                throw new ArgumentException($"Candidate node ID {id} is not in the graph.", nameof(candidateNodeIds));
            }

            if (!graph.Nodes[index].IsCandidateSite)
            {
                throw new ArgumentException($"Candidate node ID {id} is not eligible as a candidate site.", nameof(candidateNodeIds));
            }

            candidateIndices.Add(index);
        }

        var neighborhoods = new int[nodeCount][];
        foreach (var index in candidateIndices)
        {
            neighborhoods[index] = FindNeighborhood(graph, index, radiusMeters);
        }

        var isCovered = new bool[nodeCount];
        var isSelected = new bool[nodeCount];
        var coveredCount = 0;
        var selectedNodeIds = new List<long>();
        var steps = new List<CoverageStep>();

        while (coveredCount < nodeCount)
        {
            var bestCandidateIndex = -1;
            var bestGain = 0;

            foreach (var candidateIndex in candidateIndices)
            {
                if (isSelected[candidateIndex])
                {
                    continue;
                }

                var gain = 0;
                foreach (var demandIndex in neighborhoods[candidateIndex])
                {
                    if (!isCovered[demandIndex])
                    {
                        gain++;
                    }
                }

                if (gain > bestGain ||
                    (gain == bestGain && gain > 0 &&
                     (bestCandidateIndex < 0 || graph.Nodes[candidateIndex].Id < graph.Nodes[bestCandidateIndex].Id)))
                {
                    bestCandidateIndex = candidateIndex;
                    bestGain = gain;
                }
            }

            if (bestCandidateIndex < 0)
            {
                break;
            }

            isSelected[bestCandidateIndex] = true;
            var selectedNodeId = graph.Nodes[bestCandidateIndex].Id;
            selectedNodeIds.Add(selectedNodeId);
            var newlyCoveredNodeIds = new List<long>(bestGain);
            foreach (var demandIndex in neighborhoods[bestCandidateIndex])
            {
                if (!isCovered[demandIndex])
                {
                    isCovered[demandIndex] = true;
                    coveredCount++;
                    newlyCoveredNodeIds.Add(graph.Nodes[demandIndex].Id);
                }
            }

            steps.Add(new CoverageStep(steps.Count + 1, selectedNodeId, newlyCoveredNodeIds, coveredCount));
        }

        stopwatch.Stop();

        var coveredNodeIds = new List<long>(coveredCount);
        var uncoveredNodeIds = new List<long>(nodeCount - coveredCount);
        for (var index = 0; index < nodeCount; index++)
        {
            (isCovered[index] ? coveredNodeIds : uncoveredNodeIds).Add(graph.Nodes[index].Id);
        }

        coveredNodeIds.Sort();
        uncoveredNodeIds.Sort();

        return new CoverageResult(
            selectedNodeIds,
            coveredNodeIds,
            uncoveredNodeIds,
            steps,
            nodeCount,
            radiusMeters,
            stopwatch.Elapsed.TotalMilliseconds);
    }

    private static int[] FindNeighborhood(Graph graph, int sourceIndex, double radiusMeters)
    {
        var distances = new double[graph.Nodes.Count];
        Array.Fill(distances, double.PositiveInfinity);
        distances[sourceIndex] = 0;

        var queue = new PriorityQueue<int, double>();
        queue.Enqueue(sourceIndex, 0);

        while (queue.TryDequeue(out var currentIndex, out var currentDistance))
        {
            if (currentDistance > distances[currentIndex])
            {
                continue;
            }

            foreach (var edge in graph.Nodes[currentIndex].Edges)
            {
                var distance = currentDistance + edge.LengthMeters;
                if (distance > radiusMeters || distance >= distances[edge.TargetIndex])
                {
                    continue;
                }

                distances[edge.TargetIndex] = distance;
                queue.Enqueue(edge.TargetIndex, distance);
            }
        }

        var neighborhood = new List<int>();
        for (var index = 0; index < distances.Length; index++)
        {
            if (distances[index] <= radiusMeters)
            {
                neighborhood.Add(index);
            }
        }

        return neighborhood.ToArray();
    }
}