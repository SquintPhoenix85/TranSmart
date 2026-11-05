using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TranSmart.Models;
using TranSmart.Services;

namespace TranSmart.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly string _graphPath;
    private readonly RelayCommand _runCommand;
    private bool _initializationStarted;
    private bool _isBusy;
    private double _radiusMeters = 500;
    private Graph? _graph;
    private CoverageResult? _result;
    private string _statusMessage = "Loading the supplied road graph...";
    private string _mapStatusMessage = "Map is initializing...";
    private string _errorMessage = string.Empty;

    public MainWindowViewModel(string? graphPath = null)
    {
        _graphPath = graphPath ?? Path.Combine(AppContext.BaseDirectory, "amb_graph.json");
        _runCommand = new RelayCommand(() => _ = RunOptimizationAsync(), CanRunOptimization);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand RunCommand => _runCommand;

    public Graph? Graph
    {
        get => _graph;
        private set
        {
            if (SetProperty(ref _graph, value))
            {
                OnPropertyChanged(nameof(TotalVertexCount));
                _runCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public CoverageResult? Result
    {
        get => _result;
        private set
        {
            if (SetProperty(ref _result, value))
            {
                OnPropertyChanged(nameof(TotalVertexCount));
                OnPropertyChanged(nameof(SelectedSiteCount));
                OnPropertyChanged(nameof(CoveredVertexCount));
                OnPropertyChanged(nameof(UncoveredVertexCount));
                OnPropertyChanged(nameof(CoveragePercentage));
                OnPropertyChanged(nameof(NodeReductionPercentage));
                OnPropertyChanged(nameof(OptimizationTimeMilliseconds));
            }
        }
    }

    public double RadiusMeters
    {
        get => _radiusMeters;
        set
        {
            if (!double.IsFinite(value) || value is < 50 or > 500)
            {
                return;
            }

            if (SetProperty(ref _radiusMeters, value))
            {
                _runCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanChangeRadius));
                _runCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string MapStatusMessage
    {
        get => _mapStatusMessage;
        private set => SetProperty(ref _mapStatusMessage, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool CanChangeRadius => !IsBusy;

    public int TotalVertexCount => Result?.TotalNodeCount ?? Graph?.Nodes.Count ?? 0;

    public int SelectedSiteCount => Result?.SelectedNodeCount ?? 0;

    public int CoveredVertexCount => Result?.CoveredNodeCount ?? 0;

    public int UncoveredVertexCount => Result?.UncoveredNodeIds.Count ?? 0;

    public double CoveragePercentage => Result?.CoveragePercentage ?? 0;

    public double NodeReductionPercentage => Result?.NodeReductionPercentage ?? 0;

    public double OptimizationTimeMilliseconds => Result?.ElapsedMilliseconds ?? 0;

    public void SetMapStatus(string message) => MapStatusMessage = message;

    public async Task InitializeAsync()
    {
        if (_initializationStarted)
        {
            return;
        }

        _initializationStarted = true;
        IsBusy = true;
        ErrorMessage = string.Empty;
        StatusMessage = "Loading the supplied road graph...";

        try
        {
            var graph = await Task.Run(() => GraphLoader.Load(_graphPath));
            Graph = graph;
            StatusMessage = $"Graph loaded: {graph.Nodes.Count:N0} vertices are ready for analysis.";
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            StatusMessage = "Graph loading failed. Check the input file and try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RunOptimizationAsync()
    {
        if (!CanRunOptimization())
        {
            return;
        }

        var graph = Graph!;
        var radiusMeters = RadiusMeters;
        IsBusy = true;
        ErrorMessage = string.Empty;
        StatusMessage = $"Optimizing graph coverage within {radiusMeters:0} m...";

        try
        {
            Result = await Task.Run(() => CoverageOptimizer.Optimize(graph, radiusMeters));
            StatusMessage = $"Optimization complete for a {radiusMeters:0} m walking-network radius.";
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            StatusMessage = "Optimization failed. Adjust the radius or check the graph input.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRunOptimization() => Graph is not null && !IsBusy && RadiusMeters is >= 50 and <= 500;

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class RelayCommand(Action execute, Func<bool> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => canExecute();

        public void Execute(object? parameter) => execute();

        public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}