using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Microsoft.Web.WebView2.Core;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using TranSmart.ViewModels;

namespace TranSmart;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private bool _mapReady;
    private readonly DispatcherTimer _replayTimer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(850)
    };

    public MainWindowViewModel ViewModel { get; }

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainWindowViewModel();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        _replayTimer.Tick += ReplayTimer_OnTick;
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
        await InitializeMapAsync();
    }

    private void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        _replayTimer.Stop();
        _replayTimer.Tick -= ReplayTimer_OnTick;
        ViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;

        if (CoverageMap.CoreWebView2 is { } coreWebView)
        {
            coreWebView.WebMessageReceived -= CoreWebView2_OnWebMessageReceived;
        }
    }

    private async Task InitializeMapAsync()
    {
        try
        {
            var mapFolder = Path.Combine(AppContext.BaseDirectory, "Map");
            var requiredAssets = new[]
            {
                Path.Combine(mapFolder, "index.html"),
                Path.Combine(mapFolder, "leaflet", "leaflet.js"),
                Path.Combine(mapFolder, "leaflet", "leaflet.css")
            };
            var missingAsset = requiredAssets.FirstOrDefault(path => !File.Exists(path));
            if (missingAsset is not null)
            {
                throw new FileNotFoundException($"Required map asset was not found: {missingAsset}");
            }

            await CoverageMap.EnsureCoreWebView2Async();
            var coreWebView = CoverageMap.CoreWebView2;
            coreWebView.SetVirtualHostNameToFolderMapping(
                "transmart.local",
                mapFolder,
                CoreWebView2HostResourceAccessKind.Allow);
            coreWebView.WebMessageReceived += CoreWebView2_OnWebMessageReceived;
            coreWebView.Navigate("https://transmart.local/index.html");
            ViewModel.SetMapStatus("Map is starting; OpenStreetMap basemap tiles require internet access.");
        }
        catch (Exception exception)
        {
            ViewModel.SetMapStatus(
                $"Map unavailable. Ensure the Microsoft Edge WebView2 Runtime is installed and map assets are present. {exception.Message} Optimization remains available.");
        }
    }

    private void CoreWebView2_OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs eventArgs)
    {
        var message = eventArgs.TryGetWebMessageAsString();
        switch (message)
        {
            case "ready":
                _mapReady = true;
                ViewModel.SetMapStatus("Interactive map ready. OpenStreetMap basemap tiles require internet access.");
                SendMapResults();
                break;
            case "tile-error":
                ViewModel.SetMapStatus(
                    "OpenStreetMap tiles are unavailable. Check your internet connection; optimization still works offline.");
                break;
            case "map-error":
                ViewModel.SetMapStatus("Map initialization failed. Optimization remains available.");
                break;
        }
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(MainWindowViewModel.Graph) or nameof(MainWindowViewModel.Result))
        {
            SendMapResults();
        }

        if (eventArgs.PropertyName == nameof(MainWindowViewModel.ReplayPosition))
        {
            SendReplayPosition();
        }

        if (eventArgs.PropertyName is nameof(MainWindowViewModel.IsPlaying) or nameof(MainWindowViewModel.IsBusy))
        {
            UpdateReplayTimer();
        }
    }

    private void SendMapResults()
    {
        if (!_mapReady)
        {
            return;
        }

        var graph = ViewModel.Graph;
        var result = ViewModel.Result;
        var coreWebView = CoverageMap.CoreWebView2;
        if (graph is null || coreWebView is null)
        {
            return;
        }

        try
        {
            var message = JsonSerializer.Serialize(new
            {
                type = "results",
                position = ViewModel.ReplayPosition,
                animationsEnabled = SystemParameters.ClientAreaAnimation,
                nodes = graph.Nodes.Select(node => new
                {
                    id = node.Id.ToString(CultureInfo.InvariantCulture),
                    lat = node.Latitude,
                    lon = node.Longitude
                }),
                steps = result?.Steps.Select(step => new
                {
                    stepNumber = step.StepNumber,
                    selectedNodeId = step.SelectedNodeId.ToString(CultureInfo.InvariantCulture),
                    newlyCoveredNodeIds = step.NewlyCoveredNodeIds
                        .Select(id => id.ToString(CultureInfo.InvariantCulture))
                }),
                radiusMeters = result?.RadiusMeters
            });

            coreWebView.PostWebMessageAsJson(message);
        }
        catch (Exception exception)
        {
            ViewModel.SetMapStatus($"Map update failed: {exception.Message} Optimization remains available.");
        }
    }

    private void SendReplayPosition()
    {
        if (!_mapReady || CoverageMap.CoreWebView2 is not { } coreWebView)
        {
            return;
        }

        try
        {
            var message = JsonSerializer.Serialize(new
            {
                type = "replay",
                position = ViewModel.ReplayPosition,
                animationsEnabled = SystemParameters.ClientAreaAnimation
            });
            coreWebView.PostWebMessageAsJson(message);
        }
        catch (Exception exception)
        {
            ViewModel.SetMapStatus($"Map replay update failed: {exception.Message} Optimization remains available.");
        }
    }

    private void ReplayTimer_OnTick(object? sender, EventArgs eventArgs) => ViewModel.AdvanceReplay();

    private void UpdateReplayTimer()
    {
        if (ViewModel.IsPlaying && ViewModel.CanReplay)
        {
            _replayTimer.Start();
        }
        else
        {
            _replayTimer.Stop();
        }
    }
}