using System.IO;
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
    public void Radius_RejectsValuesOutsideTheDashboardRange()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.RadiusMeters = 25;

        Assert.Equal(500, viewModel.RadiusMeters);
    }
}