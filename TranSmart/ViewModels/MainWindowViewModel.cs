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
    private readonly string? _bundledGraphPath;
    private readonly IGraphGenerator _graphGenerator;
    private readonly RelayCommand _runCommand;
    private readonly RelayCommand _searchAtmsCommand;
    private readonly RelayCommand _selectCoverageModeCommand;
    private readonly RelayCommand _selectAtmModeCommand;
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
    private string _statusMessage = "Cargando el grafo de calles disponible...";
    private string _mapStatusMessage = "El mapa se está iniciando...";
    private string _errorMessage = string.Empty;
    private bool _isAtmSearchMode;
    private bool _isAtmMode;
    private IReadOnlyList<NearbyAtm> _nearbyAtms = [];
    private NearbyAtm? _selectedAtm;
    private (double Latitude, double Longitude)? _searchOrigin;

    public MainWindowViewModel(string? graphPath = null, IGraphGenerator? graphGenerator = null)
    {
        if (graphPath is null)
        {
            _graphPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TranSmart",
                "amb_graph.json");
            _bundledGraphPath = Path.Combine(AppContext.BaseDirectory, "amb_graph.json");
        }
        else
        {
            _graphPath = graphPath;
        }
        _graphGenerator = graphGenerator ?? new PythonScraperGraphGenerator();
        _runCommand = new RelayCommand(_ => StartOptimization(), _ => CanRunOptimization());
        _searchAtmsCommand = new RelayCommand(_ => ActivateAtmSearch(), _ => IsAtmMode && Graph is { Atms.Count: > 0 } && !IsBusy);
        _selectCoverageModeCommand = new RelayCommand(_ => SelectMode(false), _ => !IsBusy);
        _selectAtmModeCommand = new RelayCommand(_ => SelectMode(true), _ => !IsBusy);
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

    public ICommand SearchAtmsCommand => _searchAtmsCommand;

    public ICommand SelectCoverageModeCommand => _selectCoverageModeCommand;

    public ICommand SelectAtmModeCommand => _selectAtmModeCommand;

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
                OnPropertyChanged(nameof(CandidateSiteCount));
                OnPropertyChanged(nameof(CandidateSiteIds));
                OnPropertyChanged(nameof(AtmPanelMessage));
                OnPropertyChanged(nameof(NodeReductionPercentage));
                _runCommand.NotifyCanExecuteChanged();
                _searchAtmsCommand.NotifyCanExecuteChanged();
                _selectCoverageModeCommand.NotifyCanExecuteChanged();
                _selectAtmModeCommand.NotifyCanExecuteChanged();
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
                _searchAtmsCommand.NotifyCanExecuteChanged();
                _generateGraphCommand.NotifyCanExecuteChanged();
                _selectCoverageModeCommand.NotifyCanExecuteChanged();
                _selectAtmModeCommand.NotifyCanExecuteChanged();
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

    public bool IsAtmSearchMode
    {
        get => _isAtmSearchMode;
        private set => SetProperty(ref _isAtmSearchMode, value);
    }

    public bool IsAtmMode
    {
        get => _isAtmMode;
        private set
        {
            if (SetProperty(ref _isAtmMode, value))
            {
                OnPropertyChanged(nameof(IsCoverageMode));
                OnPropertyChanged(nameof(MapTitle));
                OnPropertyChanged(nameof(AtmPanelMessage));
                _searchAtmsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsCoverageMode => !IsAtmMode;

    public string MapTitle => IsAtmMode ? "Mapa de cajeros automáticos" : "Mapa de cubrimiento";

    public string AtmPanelMessage => IsAtmSearchMode
        ? "Haz clic en el mapa para elegir el punto de partida."
        : NearbyAtms.Count > 0
            ? "Selecciona un cajero de la lista para centrarlo y resaltarlo en el mapa."
            : Graph is null
                ? "Carga o actualiza los datos de OpenStreetMap para buscar cajeros."
                : Graph.Atms.Count == 0
                    ? "El grafo cargado no contiene cajeros. Pulsa “Actualizar datos OSM” para descargarlos."
                    : $"Hay {Graph.Atms.Count:N0} cajeros registrados. Pulsa “Elegir punto en el mapa” y luego marca tu ubicación.";

    public NearbyAtm? SelectedAtm
    {
        get => _selectedAtm;
        set
        {
            if (value is not null && !_nearbyAtms.Contains(value))
            {
                return;
            }

            if (SetProperty(ref _selectedAtm, value) && value is not null)
            {
                MapStatusMessage = $"Cajero seleccionado: {value.Atm.Name}, a {value.WalkingDistanceMeters:0} m a pie.";
            }
        }
    }

    public bool CanChangeRadius => !IsBusy;

    public bool CanGenerateGraph => !IsBusy;

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
        ? "Ejecuta una optimización para registrar las decisiones del algoritmo voraz."
        : CurrentReplayStep is { } step
            ? $"Paso {step.StepNumber} de {ReplayStepCount}: la ubicación {step.SelectedNodeId} añade {step.Gain:N0} vértices nuevos."
            : ReplayStepCount == 0
                ? "Este resultado no tiene decisiones para reproducir."
                : "Listo para reproducir. Inicia desde el comienzo o elige una decisión de la lista.";

    public string PlaybackButtonText => IsPlaying ? "Pausar" : "Reproducir";

    public string SolvedRadiusText => Result is null ? "—" : $"{Result.RadiusMeters:0} m";

    public int TotalVertexCount => Result?.TotalNodeCount ?? Graph?.Nodes.Count ?? 0;

    public IEnumerable<long> CandidateSiteIds => Graph is { } graph ? graph.CandidateSiteIds : Enumerable.Empty<long>();

    public int CandidateSiteCount => Graph?.CandidateSiteCount ?? 0;

    public int SelectedSiteCount => Result?.SelectedNodeCount ?? 0;

    public int CoveredVertexCount => Result?.CoveredNodeCount ?? 0;

    public int UncoveredVertexCount => Result?.UncoveredNodeIds.Count ?? 0;

    public double CoveragePercentage => Result?.CoveragePercentage ?? 0;

    public double NodeReductionPercentage => Result is null || CandidateSiteCount == 0
        ? 0
        : (double)(CandidateSiteCount - SelectedSiteCount) / CandidateSiteCount * 100;

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

    public void ActivateAtmSearch()
    {
        if (!IsAtmMode || Graph is null || IsBusy) return;
        _nearbyAtms = [];
        SelectedAtm = null;
        _searchOrigin = null;
        OnPropertyChanged(nameof(NearbyAtms));
        IsAtmSearchMode = true;
        OnPropertyChanged(nameof(AtmPanelMessage));
        MapStatusMessage = "Modo cajeros activo: haz clic en el mapa para elegir tu punto de partida.";
    }

    public void HandleMapPoint(double latitude, double longitude)
    {
        if (!IsAtmSearchMode || Graph is null) return;
        IsAtmSearchMode = false;
        OnPropertyChanged(nameof(AtmPanelMessage));
        _searchOrigin = (latitude, longitude);
        try
        {
            _nearbyAtms = AtmFinder.FindNearest(Graph, latitude, longitude);
            OnPropertyChanged(nameof(NearbyAtms));
            SelectedAtm = _nearbyAtms.FirstOrDefault();
            OnPropertyChanged(nameof(AtmPanelMessage));
            MapStatusMessage = _nearbyAtms.Count == 0
                ? "No hay cajeros registrados en esta zona de OpenStreetMap, o no se encontró una ruta peatonal."
                : $"Se encontraron {_nearbyAtms.Count} cajeros cercanos, ordenados por distancia estimada caminando.";
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            MapStatusMessage = "No se pudo calcular la búsqueda de cajeros.";
        }
    }

    public IReadOnlyList<NearbyAtm> NearbyAtms => _nearbyAtms;

    public (double Latitude, double Longitude)? SearchOrigin => _searchOrigin;

    private void SelectMode(bool atmMode)
    {
        IsAtmMode = atmMode;
        IsAtmSearchMode = false;
        _nearbyAtms = [];
        SelectedAtm = null;
        _searchOrigin = null;
        OnPropertyChanged(nameof(NearbyAtms));
        OnPropertyChanged(nameof(AtmPanelMessage));
        MapStatusMessage = atmMode
            ? "Modo cajeros: carga datos OSM si el grafo aún no contiene cajeros."
            : "Modo optimización de cobertura.";
    }

    public async Task InitializeAsync()
    {
        if (_initializationStarted)
        {
            return;
        }

        _initializationStarted = true;
        IsBusy = true;
        ErrorMessage = string.Empty;
        StatusMessage = "Cargando el grafo de calles disponible...";

        try
        {
            if (!File.Exists(_graphPath) && _bundledGraphPath is not null && File.Exists(_bundledGraphPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_graphPath)!);
                File.Copy(_bundledGraphPath, _graphPath);
            }

            var graph = await Task.Run(() => GraphLoader.Load(_graphPath));
            Graph = graph;
            IsGraphMissing = false;
            StatusMessage = $"Grafo cargado: {graph.Nodes.Count:N0} vértices y {graph.Atms.Count:N0} cajeros de OpenStreetMap disponibles.";
        }
        catch (Exception exception)
        {
            IsGraphMissing = true;
            ErrorMessage = exception.Message;
            StatusMessage = "No se pudo cargar el grafo. Revisa el archivo de entrada e inténtalo de nuevo.";
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
            StatusMessage = $"Grafo actualizado: {graph.Nodes.Count:N0} vértices y {graph.Atms.Count:N0} cajeros de OpenStreetMap disponibles.";
        }
        catch (Exception exception)
        {
            IsGraphMissing = Graph is null;
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
        StatusMessage = $"Optimizando la cobertura del grafo en un radio de {radiusMeters:0} m...";

        try
        {
            Result = await Task.Run(() => CoverageOptimizer.Optimize(graph, radiusMeters, graph.CandidateSiteIds));
            StatusMessage = Result.IsFeasible
                ? $"Optimización completada para un radio peatonal de {radiusMeters:0} m."
                : $"No es posible cubrir todos los nodos con un radio de {radiusMeters:0} m: quedan {Result.UncoveredNodeIds.Count:N0} nodos de demanda fuera del alcance de las ubicaciones candidatas.";
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            StatusMessage = "No se pudo optimizar. Ajusta el radio o revisa el grafo de entrada.";
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
