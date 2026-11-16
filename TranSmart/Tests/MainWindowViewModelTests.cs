using System.IO;
using TranSmart.Services;
using TranSmart.ViewModels;
using Xunit;

namespace TranSmart.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task InitializeAndRepeatedRuns_UpdateBusyStateAndMetrics()
    {
        var graphPath = Path.Combine(AppContext.BaseDirectory, "amb_graph.json");
        var viewModel = new MainWindowViewModel(graphPath);
        var busyStates = new List<bool>();
        viewModel.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(MainWindowViewModel.IsBusy))
            {
                busyStates.Add(viewModel.IsBusy);
            }
        };

        await viewModel.InitializeAsync();

        Assert.NotNull(viewModel.Graph);
        Assert.Equal(viewModel.Graph.Nodes.Count, viewModel.TotalVertexCount);
        Assert.Equal(viewModel.Graph.CandidateSiteCount, viewModel.CandidateSiteCount);
        Assert.Equal(viewModel.Graph.CandidateSiteIds, viewModel.CandidateSiteIds);
        Assert.Equal(500, viewModel.RadiusMeters);
        Assert.True(viewModel.RunCommand.CanExecute(null));
        Assert.Empty(viewModel.ErrorMessage);

        var firstRun = viewModel.RunOptimizationAsync();
        var overlappingRun = viewModel.RunOptimizationAsync();
        Assert.False(viewModel.RunCommand.CanExecute(null));
        await Task.WhenAll(firstRun, overlappingRun);

        var firstResult = viewModel.Result;
        Assert.NotNull(firstResult);
        Assert.False(viewModel.IsBusy);
        Assert.Equal(viewModel.TotalVertexCount, viewModel.CoveredVertexCount + viewModel.UncoveredVertexCount);
        Assert.Equal(100.0 * viewModel.CoveredVertexCount / viewModel.TotalVertexCount, viewModel.CoveragePercentage, 8);
        Assert.Equal(viewModel.CandidateSiteCount == 0 ? 0 : 100.0 * (viewModel.CandidateSiteCount - viewModel.SelectedSiteCount) / viewModel.CandidateSiteCount,
            viewModel.NodeReductionPercentage, 8);

        viewModel.RadiusMeters = 50;
        await viewModel.RunOptimizationAsync();

        Assert.NotNull(viewModel.Result);
        Assert.NotSame(firstResult, viewModel.Result);
        Assert.Equal(50, viewModel.Result.RadiusMeters);
        Assert.False(viewModel.IsBusy);
        Assert.Contains(true, busyStates);
        Assert.False(busyStates[^1]);
    }

    [Theory]
    [InlineData(true, true, false, 2, 2, 3, 0, 0)]
    [InlineData(true, false, true, 2, 1, 2, 1, 50)]
    [InlineData(true, false, false, 1, 1, 2, 1, 0)]
    [InlineData(false, false, false, 0, 0, 0, 3, 0)]
    public async Task RunOptimization_UsesEligibleSitesAndReportsIncompleteCoverage(
        bool firstCandidate, bool secondCandidate, bool thirdCandidate,
        int candidateCount, int selectedCount, int coveredCount, int uncoveredCount, double reduction)
    {
        var graphPath = Path.Combine(Path.GetTempPath(), $"eligibility-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(graphPath, $$"""
                {
                  "schemaVersion": 2,
                  "networkType": "walk",
                  "nodes": [
                    { "id": 1, "lat": 11.0, "lon": -74.8, "isCandidateSite": {{firstCandidate.ToString().ToLowerInvariant()}} },
                    { "id": 2, "lat": 11.001, "lon": -74.801, "isCandidateSite": {{secondCandidate.ToString().ToLowerInvariant()}} },
                    { "id": 3, "lat": 11.002, "lon": -74.802, "isCandidateSite": {{thirdCandidate.ToString().ToLowerInvariant()}} }
                  ],
                  "edges": [{ "source": 1, "target": 3, "length": 30 }]
                }
                """);
            var viewModel = new MainWindowViewModel(graphPath);
            await viewModel.InitializeAsync();
            viewModel.RadiusMeters = 50;
            await viewModel.RunOptimizationAsync();

            Assert.NotNull(viewModel.Result);
            Assert.Equal(3, viewModel.TotalVertexCount);
            Assert.Equal(candidateCount, viewModel.CandidateSiteCount);
            Assert.Equal(new[] { (1L, firstCandidate), (2L, secondCandidate), (3L, thirdCandidate) }
                .Where(candidate => candidate.Item2).Select(candidate => candidate.Item1), viewModel.CandidateSiteIds);
            Assert.All(viewModel.Result.SelectedNodeIds, id => Assert.Contains(id, viewModel.CandidateSiteIds));
            Assert.Equal(selectedCount, viewModel.SelectedSiteCount);
            Assert.Equal(coveredCount, viewModel.CoveredVertexCount);
            Assert.Equal(uncoveredCount, viewModel.UncoveredVertexCount);
            Assert.Equal(100.0 * coveredCount / 3, viewModel.CoveragePercentage, 8);
            Assert.Equal(reduction, viewModel.NodeReductionPercentage, 8);
            if (uncoveredCount > 0)
            {
                Assert.Contains("infeasible", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(uncoveredCount.ToString(), viewModel.StatusMessage);
            }
            else
            {
                Assert.Contains("complete", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            File.Delete(graphPath);
        }
    }

    [Fact]
    public async Task Initialize_MissingGraphReportsErrorAndKeepsOptimizationUnavailable()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");
        var viewModel = new MainWindowViewModel(missingPath);

        await viewModel.InitializeAsync();

        Assert.Null(viewModel.Graph);
        Assert.NotEmpty(viewModel.ErrorMessage);
        Assert.Contains("failed", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.RunCommand.CanExecute(null));
    }

    [Fact]
    public async Task GenerateGraph_WhenGraphIsMissing_GeneratesAndLoadsIt()
    {
        var graphPath = Path.Combine(Path.GetTempPath(), $"generated-{Guid.NewGuid():N}.json");
        var generator = new FakeGraphGenerator();
        var viewModel = new MainWindowViewModel(graphPath, generator);

        try
        {
            await viewModel.InitializeAsync();
            Assert.True(viewModel.IsGraphMissing);
            Assert.True(viewModel.CanGenerateGraph);

            await viewModel.GenerateGraphAsync();

            Assert.NotNull(viewModel.Graph);
            Assert.Equal(2, viewModel.Graph.Nodes.Count);
            Assert.False(viewModel.IsGraphMissing);
            Assert.True(viewModel.CanGenerateGraph);
            Assert.True(viewModel.GenerateGraphCommand.CanExecute(null));
            Assert.Empty(viewModel.ErrorMessage);
            Assert.True(viewModel.RunCommand.CanExecute(null));
            Assert.Equal(graphPath, generator.LastOutputPath);
        }
        finally
        {
            File.Delete(graphPath);
        }
    }

    [Fact]
    public async Task GenerateGraph_WhenGraphIsLoaded_CanRefreshAndDisablesGenerationWhileRunning()
    {
        var graphPath = Path.Combine(Path.GetTempPath(), $"refresh-{Guid.NewGuid():N}.json");
        var generator = new FakeGraphGenerator();
        var viewModel = new MainWindowViewModel(graphPath, generator);
        TaskCompletionSource<bool>? generationGate = null;
        Task? refreshTask = null;

        try
        {
            await viewModel.InitializeAsync();
            await viewModel.GenerateGraphAsync();
            Assert.NotNull(viewModel.Graph);
            Assert.False(viewModel.IsGraphMissing);

            var generationStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            generationGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            generator.GenerationStarted = generationStarted;
            generator.GenerationGate = generationGate;

            refreshTask = viewModel.GenerateGraphAsync();
            await generationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(viewModel.IsBusy);
            Assert.False(viewModel.CanGenerateGraph);
            Assert.False(viewModel.GenerateGraphCommand.CanExecute(null));

            generationGate.SetResult(true);
            await refreshTask;

            Assert.NotNull(viewModel.Graph);
            Assert.False(viewModel.IsGraphMissing);
            Assert.True(viewModel.CanGenerateGraph);
            Assert.True(viewModel.GenerateGraphCommand.CanExecute(null));
            Assert.Equal(2, generator.AttemptCount);
        }
        finally
        {
            generationGate?.TrySetResult(true);
            if (refreshTask is not null)
            {
                await refreshTask;
            }
            File.Delete(graphPath);
        }
    }

    [Fact]
    public async Task GenerateGraph_WhenGenerationFails_KeepsRetryAvailable()
    {
        var graphPath = Path.Combine(Path.GetTempPath(), $"retry-{Guid.NewGuid():N}.json");
        var generator = new FakeGraphGenerator { FailuresRemaining = 1 };
        var viewModel = new MainWindowViewModel(graphPath, generator);
        await viewModel.InitializeAsync();

        await viewModel.GenerateGraphAsync();

        Assert.Null(viewModel.Graph);
        Assert.True(viewModel.IsGraphMissing);
        Assert.True(viewModel.CanGenerateGraph);
        Assert.True(viewModel.GenerateGraphCommand.CanExecute(null));
        Assert.Contains("failed", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("test generation failure", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        await viewModel.GenerateGraphAsync();

        Assert.NotNull(viewModel.Graph);
        Assert.Equal(2, generator.AttemptCount);
        Assert.False(viewModel.IsGraphMissing);
        Assert.Empty(viewModel.ErrorMessage);

        File.Delete(graphPath);
    }

    [Fact]
    public void Radius_RejectsValuesOutsideTheDashboardRange()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.RadiusMeters = 25;

        Assert.Equal(500, viewModel.RadiusMeters);
    }

    [Fact]
    public async Task ReplayCommands_NavigateDecisionHistoryAndStopAtTheEnd()
    {
        var graphPath = Path.Combine(AppContext.BaseDirectory, "amb_graph.json");
        var viewModel = new MainWindowViewModel(graphPath);
        await viewModel.InitializeAsync();
        viewModel.RadiusMeters = 50;
        await viewModel.RunOptimizationAsync();

        var result = Assert.IsType<TranSmart.Models.CoverageResult>(viewModel.Result);
        Assert.True(result.Steps.Count > 1);
        Assert.Equal(0, viewModel.ReplayPosition);
        Assert.Equal(0, viewModel.ReplayCoveredVertexCount);
        Assert.True(viewModel.CanReplay);

        viewModel.NextStepCommand.Execute(null);
        Assert.Equal(1, viewModel.ReplayPosition);
        Assert.Equal(result.Steps[0].CoveredVertexCount, viewModel.ReplayCoveredVertexCount);

        viewModel.SelectReplayStepCommand.Execute(2);
        Assert.Equal(2, viewModel.ReplayPosition);
        viewModel.PreviousStepCommand.Execute(null);
        Assert.Equal(1, viewModel.ReplayPosition);
        viewModel.GoToEndCommand.Execute(null);
        Assert.Equal(result.Steps.Count, viewModel.ReplayPosition);
        Assert.Equal(result.CoveredNodeCount, viewModel.ReplayCoveredVertexCount);
        Assert.False(viewModel.IsPlaying);

        viewModel.GoToStartCommand.Execute(null);
        Assert.Equal(0, viewModel.ReplayPosition);
        viewModel.PlayPauseCommand.Execute(null);
        Assert.True(viewModel.IsPlaying);
        viewModel.AdvanceReplay();
        Assert.Equal(1, viewModel.ReplayPosition);

        viewModel.RadiusMeters = 100;
        viewModel.GoToEndCommand.Execute(null);
        var finalPosition = viewModel.ReplayPosition;
        var rerun = viewModel.RunOptimizationAsync();
        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.CanReplay);
        viewModel.GoToStartCommand.Execute(null);
        Assert.Equal(finalPosition, viewModel.ReplayPosition);
        await rerun;
        Assert.Equal(0, viewModel.ReplayPosition);
        Assert.False(viewModel.IsPlaying);
    }

    private sealed class FakeGraphGenerator : IGraphGenerator
    {
        private const string GraphJson = """
            {
              "schemaVersion": 2,
              "networkType": "walk",
              "nodes": [
                { "id": 1, "lat": 11.0, "lon": -74.8, "isCandidateSite": true },
                { "id": 2, "lat": 11.001, "lon": -74.801, "isCandidateSite": true }
              ],
              "edges": [
                { "source": 1, "target": 2, "length": 100 }
              ]
            }
            """;

        public int FailuresRemaining { get; set; }

        public int AttemptCount { get; private set; }

        public string? LastOutputPath { get; private set; }

        public TaskCompletionSource<bool>? GenerationStarted { get; set; }

        public TaskCompletionSource<bool>? GenerationGate { get; set; }

        public async Task GenerateAsync(
            string outputPath,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            AttemptCount++;
            LastOutputPath = outputPath;
            if (FailuresRemaining > 0)
            {
                FailuresRemaining--;
                throw new InvalidOperationException("Test generation failure.");
            }

            if (GenerationGate is { } generationGate)
            {
                GenerationStarted?.TrySetResult(true);
                await generationGate.Task.WaitAsync(cancellationToken);
            }

            progress?.Report("Test graph created.");
            await File.WriteAllTextAsync(outputPath, GraphJson, cancellationToken);
        }
    }
}