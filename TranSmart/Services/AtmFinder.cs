using TranSmart.Models;

namespace TranSmart.Services;

public sealed record NearbyAtm(AtmLocation Atm, double WalkingDistanceMeters, int Rank);

public static class AtmFinder
{
    public static IReadOnlyList<NearbyAtm> FindNearest(Graph graph, double latitude, double longitude, int limit = 10)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90 ||
            !double.IsFinite(longitude) || longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(latitude), "La ubicación seleccionada no es válida.");
        if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit));
        if (graph.Atms.Count == 0 || graph.Nodes.Count == 0) return [];

        var originIndex = FindNearestNode(graph, latitude, longitude);
        var distances = Dijkstra(graph, originIndex);
        return graph.Atms
            .Select(atm =>
            {
                var nodeIndex = FindNearestNode(graph, atm.Latitude, atm.Longitude);
                return (Atm: atm, Distance: distances[nodeIndex]);
            })
            .Where(result => double.IsFinite(result.Distance))
            .OrderBy(result => result.Distance)
            .ThenBy(result => result.Atm.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(limit)
            .Select((result, index) => new NearbyAtm(result.Atm, result.Distance, index + 1))
            .ToArray();
    }

    private static int FindNearestNode(Graph graph, double latitude, double longitude)
    {
        var bestIndex = 0;
        var bestDistance = double.PositiveInfinity;
        for (var index = 0; index < graph.Nodes.Count; index++)
        {
            var node = graph.Nodes[index];
            var latDistance = node.Latitude - latitude;
            var lonDistance = (node.Longitude - longitude) * Math.Cos(latitude * Math.PI / 180);
            var distance = latDistance * latDistance + lonDistance * lonDistance;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }
        return bestIndex;
    }

    private static double[] Dijkstra(Graph graph, int sourceIndex)
    {
        var distances = Enumerable.Repeat(double.PositiveInfinity, graph.Nodes.Count).ToArray();
        distances[sourceIndex] = 0;
        var queue = new PriorityQueue<int, double>();
        queue.Enqueue(sourceIndex, 0);
        while (queue.TryDequeue(out var current, out var distance))
        {
            if (distance > distances[current]) continue;
            foreach (var edge in graph.Nodes[current].Edges)
            {
                var nextDistance = distance + edge.LengthMeters;
                if (nextDistance >= distances[edge.TargetIndex]) continue;
                distances[edge.TargetIndex] = nextDistance;
                queue.Enqueue(edge.TargetIndex, nextDistance);
            }
        }
        return distances;
    }
}
