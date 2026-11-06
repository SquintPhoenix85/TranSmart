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
    private readonly IGraphGenerator _graphGenerator;
    private readonly RelayCommand _runCommand;
    private readonly RelayCommand _generateGraphCommand;
    private readonly RelayCommand _previousStepCommand;
    private readonly RelayCommand _nextStepCommand;
    private readonly RelayCommand _playPauseCommand;
    private readonly RelayCommand _goToStartCommand;
    private readonly RelayCommand _goToEndCommand;
    private readonly RelayCommand _selectReplayStepCommand;
    private bool _initializationStarted;
    private bool _isGraphMissing;
    private bool _isBusy;
    private bool _isPlaying;
    private double _radiusMeters = 500;
    private int _replayPosition;
    private Graph? _graph;
    private CoverageResult? _result;
    private string _statusMessage = "Loading the supplied road graph...";
    private string _mapStatusMessage = "Map is initializing...";
    private string _errorMessage = string.Empty;

    public MainWindowViewModel(string? graphPath = null, IGraphGenerator? graphGenerator = null)
    {
        _graphPath = graphPath ?? Path.Combine(AppContext.BaseDirectory, "amb_graph.json");
        _graphGenerator = graphGenerator ?? new PythonScraperGraphGenerator();
        _runCommand = new RelayCommand(_ => StartOptimization(), _ => CanRunOptimization());
        _generateGraphCommand = new RelayCommand(_ => StartGraphGeneration(), _ => CanGenerateGraph);
        _previousStepCommand = new RelayCommand(_ => ChangeReplayPosition(ReplayPosition - 1), _ => CanReplay && ReplayPosition > 0);
        _nextStepCommand = new RelayCommand(_ => ChangeReplayPosition(ReplayPosition + 1), _ => CanReplay && ReplayPosition < ReplayStepCount);
        _playPauseCommand = new RelayCommand(_ => TogglePlayback(), _ => CanReplay);
        _goToStartCommand = new RelayCommand(_ => ChangeReplayPosition(0), _ => CanReplay && ReplayPosition > 0);
        _goToEndCommand = new RelayCommand(_ => ChangeReplayPosition(ReplayStepCount), _ => CanReplay && ReplayPosition < ReplayStepCount);
        _selectReplayStepCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is int position)
                {
                    ChangeReplayPosition(position);
                }
            },
            parameter => CanReplay && parameter is int position && position is > 0 && position <= ReplayStepCount);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand RunCommand => _runCommand;

    public ICommand GenerateGraphCommand => _generateGraphCommand;

    public ICommand PreviousStepCommand => _previousStepCommand;

    public ICommand NextStepCommand => _nextStepCommand;

    public ICommand PlayPauseCommand => _playPauseCommand;

    public ICommand GoToStartCommand => _goToStartCommand;

    public ICommand GoToEndCommand => _goToEndCommand;

    public ICommand SelectReplayStepCommand => _selectReplayStepCommand;

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
            if (ReferenceEquals(_result, value))
            {
                return;
            }

            _result = value;
            _replayPosition = 0;
            IsPlaying = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TotalVertexCount));
            OnPropertyChanged(nameof(SelectedSiteCount));
            OnPropertyChanged(nameof(CoveredVertexCount));
            OnPropertyChanged(nameof(UncoveredVertexCount));
            OnPropertyChanged(nameof(CoveragePercentage));
            OnPropertyChanged(nameof(NodeReductionPercentage));
            OnPropertyChanged(nameof(OptimizationTimeMilliseconds));
            OnPropertyChanged(nameof(SolvedRadiusText));
            OnPropertyChanged(nameof(ReplayStepCount));
            OnPropertyChanged(nameof(ReplayPosition));
            OnPropertyChanged(nameof(ReplaySelectedSiteCount));
            OnPropertyChanged(nameof(ReplayCoveredVertexCount));
            OnPropertyChanged(nameof(ReplayCoveragePercentage));
            OnPropertyChanged(nameof(CurrentReplayStep));
            OnPropertyChanged(nameof(ReplayStepSummary));
            OnPropertyChanged(nameof(CanReplay));
            NotifyReplayCommands();
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
                if (value)
                {
                    IsPlaying = false;
                }

                OnPropertyChanged(nameof(CanChangeRadius));
                OnPropertyChanged(nameof(CanReplay));
                OnPropertyChanged(nameof(CanGenerateGraph));
                _runCommand.NotifyCanExecuteChanged();
                _generateGraphCommand.NotifyCanExecuteChanged();
                NotifyReplayCommands();
            }
        }
    }

    public bool IsGraphMissing
    {
        get => _isGraphMissing;
        private set
        {
            if (SetProperty(ref _isGraphMissing, value))
            {
                OnPropertyChanged(nameof(CanGenerateGraph));
                _generateGraphCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                OnPropertyChanged(nameof(PlaybackButtonText));
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

    public bool CanGenerateGraph => IsGraphMissing && !IsBusy;

    public bool CanReplay => Result is { Steps.Count: > 0 } && !IsBusy;

    public int ReplayPosition
    {
        get => _replayPosition;
        set
        {
            if (CanReplay)
            {
                ChangeReplayPosition(value);
            }
        }
    }

    public int ReplayStepCount => Result?.Steps.Count ?? 0;

    public int ReplaySelectedSiteCount => ReplayPosition;

    public int ReplayCoveredVertexCount => CurrentReplayStep?.CoveredVertexCount ?? 0;

    public double ReplayCoveragePercentage => TotalVertexCount == 0
        ? 0
        : (double)ReplayCoveredVertexCount / TotalVertexCount * 100;

    public CoverageStep? CurrentReplayStep => ReplayPosition == 0 ? null : Result?.Steps[ReplayPosition - 1];

    public string ReplayStepSummary => Result is null
        ? "Run an optimization to record the greedy decisions."
        : CurrentReplayStep is { } step
            ? $"Step {step.StepNumber} of {ReplayStepCount}: site {step.SelectedNodeId} adds {step.Gain:N0} new vertices."
            : ReplayStepCount == 0
                ? "This result has no decisions to replay."
                : "Ready to replay. Start from the baseline or choose a decision below.";

    public string PlaybackButtonText => IsPlaying ? "Pause" : "Play";

    public string SolvedRadiusText => Result is null ? "—" : $"{Result.RadiusMeters:0} m";

    public int TotalVertexCount => Result?.TotalNodeCount ?? Graph?.Nodes.Count ?? 0;

    public int SelectedSiteCount => Result?.SelectedNodeCount ?? 0;

    public int CoveredVertexCount => Result?.CoveredNodeCount ?? 0;

    public int UncoveredVertexCount => Result?.UncoveredNodeIds.Count ?? 0;

    public double CoveragePercentage => Result?.CoveragePercentage ?? 0;

    public double NodeReductionPercentage => Result?.NodeReductionPercentage ?? 0;

    public double OptimizationTimeMilliseconds => Result?.ElapsedMilliseconds ?? 0;

    public void AdvanceReplay()
    {
        if (!IsPlaying || !CanReplay)
        {
            IsPlaying = false;
            return;
        }

        SetReplayPosition(ReplayPosition + 1);
        if (ReplayPosition >= ReplayStepCount)
        {
            IsPlaying = false;
        }
    }

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
            IsGraphMissing = false;
            StatusMessage = $"Graph loaded: {graph.Nodes.Count:N0} vertices are ready for analysis.";
        }
        catch (Exception exception)
        {
            IsGraphMissing = true;
            ErrorMessage = exception.Message;
            StatusMessage = "Graph loading failed. Check the input file and try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task GenerateGraphAsync()
    {
        if (!CanGenerateGraph)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        StatusMessage = "Generating the road graph from OpenStreetMap...";

        try
        {
            var progress = new Progress<string>(message => StatusMessage = message);
            await _graphGenerator.GenerateAsync(_graphPath, progress);
            var graph = await Task.Run(() => GraphLoader.Load(_graphPath));
            Graph = graph;
            IsGraphMissing = false;
            StatusMessage = $"Graph generated and loaded: {graph.Nodes.Count:N0} vertices are ready for analysis.";
        }
        catch (Exception exception)
        {
            IsGraphMissing = true;
            ErrorMessage = exception.Message;
            StatusMessage = "Graph generation failed. Check Python, OSMnx, and internet access, then try again.";
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

    private void StartOptimization() => _ = RunOptimizationAsync();

    private void StartGraphGeneration() => _ = GenerateGraphAsync();

    private void TogglePlayback()
    {
        if (IsPlaying)
        {
            IsPlaying = false;
            return;
        }

        if (ReplayPosition >= ReplayStepCount)
        {
            SetReplayPosition(0);
        }

        IsPlaying = CanReplay && ReplayPosition < ReplayStepCount;
    }

    private void ChangeReplayPosition(int position)
    {
        IsPlaying = false;
        SetReplayPosition(position);
    }

    private void SetReplayPosition(int position)
    {
        var clampedPosition = Math.Clamp(position, 0, ReplayStepCount);
        if (_replayPosition == clampedPosition)
        {
            return;
        }

        _replayPosition = clampedPosition;
        OnPropertyChanged(nameof(ReplayPosition));
        OnPropertyChanged(nameof(ReplaySelectedSiteCount));
        OnPropertyChanged(nameof(ReplayCoveredVertexCount));
        OnPropertyChanged(nameof(ReplayCoveragePercentage));
        OnPropertyChanged(nameof(CurrentReplayStep));
        OnPropertyChanged(nameof(ReplayStepSummary));
        NotifyReplayCommands();
    }

    private void NotifyReplayCommands()
    {
        _previousStepCommand.NotifyCanExecuteChanged();
        _nextStepCommand.NotifyCanExecuteChanged();
        _playPauseCommand.NotifyCanExecuteChanged();
        _goToStartCommand.NotifyCanExecuteChanged();
        _goToEndCommand.NotifyCanExecuteChanged();
        _selectReplayStepCommand.NotifyCanExecuteChanged();
    }

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

    private sealed class RelayCommand(Action<object?> execute, Func<object?, bool> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => canExecute(parameter);

        public void Execute(object? parameter)
        {
            if (CanExecute(parameter))
            {
                execute(parameter);
            }
        }

        public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}