using System.Diagnostics;
using TranSmart.Models;

namespace TranSmart.Services;

public static class CoverageOptimizer
{
    public static CoverageResult Optimize(Graph graph, double radiusMeters)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (!double.IsFinite(radiusMeters) || radiusMeters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusMeters), "Radius must be finite and nonnegative.");
        }

        var stopwatch = Stopwatch.StartNew();
        var nodeCount = graph.Nodes.Count;
        var neighborhoods = new int[nodeCount][];
        for (var index = 0; index < nodeCount; index++)
        {
            neighborhoods[index] = FindNeighborhood(graph, index, radiusMeters);
        }

        var isCovered = new bool[nodeCount];
        var isSelected = new bool[nodeCount];
        var coveredCount = 0;
        var selectedNodeIds = new List<long>();

        while (coveredCount < nodeCount)
        {
            var bestCandidateIndex = -1;
            var bestGain = 0;

            for (var candidateIndex = 0; candidateIndex < nodeCount; candidateIndex++)
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
                throw new InvalidOperationException(
                    "Unable to cover every graph vertex with the available candidates.");
            }

            isSelected[bestCandidateIndex] = true;
            selectedNodeIds.Add(graph.Nodes[bestCandidateIndex].Id);
            foreach (var demandIndex in neighborhoods[bestCandidateIndex])
            {
                if (!isCovered[demandIndex])
                {
                    isCovered[demandIndex] = true;
                    coveredCount++;
                }
            }
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