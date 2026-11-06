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
        Assert.Equal(852, viewModel.TotalVertexCount);
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
        Assert.Equal(viewModel.TotalVertexCount, viewModel.CoveredVertexCount);

        viewModel.RadiusMeters = 50;
        await viewModel.RunOptimizationAsync();

        Assert.NotNull(viewModel.Result);
        Assert.NotSame(firstResult, viewModel.Result);
        Assert.Equal(50, viewModel.Result.RadiusMeters);
        Assert.False(viewModel.IsBusy);
        Assert.Contains(true, busyStates);
        Assert.False(busyStates[^1]);
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

        await viewModel.InitializeAsync();
        Assert.True(viewModel.IsGraphMissing);
        Assert.True(viewModel.CanGenerateGraph);

        await viewModel.GenerateGraphAsync();

        Assert.NotNull(viewModel.Graph);
        Assert.Equal(2, viewModel.Graph.Nodes.Count);
        Assert.False(viewModel.IsGraphMissing);
        Assert.False(viewModel.CanGenerateGraph);
        Assert.Empty(viewModel.ErrorMessage);
        Assert.True(viewModel.RunCommand.CanExecute(null));
        Assert.Equal(graphPath, generator.LastOutputPath);

        File.Delete(graphPath);
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
              "nodes": [
                { "id": 1, "lat": 11.0, "lon": -74.8 },
                { "id": 2, "lat": 11.001, "lon": -74.801 }
              ],
              "edges": [
                { "source": 1, "target": 2, "length": 100 }
              ]
            }
            """;

        public int FailuresRemaining { get; set; }

        public int AttemptCount { get; private set; }

        public string? LastOutputPath { get; private set; }

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

            progress?.Report("Test graph created.");
            await File.WriteAllTextAsync(outputPath, GraphJson, cancellationToken);
        }
    }
}