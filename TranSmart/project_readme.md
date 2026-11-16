# TranSmart — Transit Network Coverage (Barranquilla North Corridor)

**Course project:** Discrete Mathematics (MAT 4021), Universidad del Norte  
**Purpose:** Explore a greedy set-cover heuristic on a real OpenStreetMap walking network, with a Windows desktop interface for coverage analysis and nearby ATM lookup.

## Project overview

TranSmart models the simplified pedestrian network around Universidad del Norte and Barranquilla's northern corridor as a weighted graph. In coverage mode, each graph vertex is one equally weighted demand point. The application selects eligible graph vertices as sites so that demand vertices are within a configurable walking-network distance of a selected site.

The academic use case is to explore possible locations for transit access, recharge, or transfer points. Selected vertices are model locations only; the application does not verify existing transit services or facilities.

The application also has an ATM mode. It can download OpenStreetMap ATM features, accept an origin selected on the map, and list up to ten nearby ATMs by estimated walking distance through the graph.

## Study area and graph model

The extractor uses this bounding box:

| Boundary | Coordinate |
| --- | --- |
| North | 11.025° N |
| South | 10.995° N |
| West | 74.835° W |
| East | 74.810° W |

The model is built as follows:

- **Demand vertices (`V`):** all walking-network nodes, each counted equally. They are not people or population estimates.
- **Eligible sites (`C ⊆ V`):** nodes with at least three distinct incoming or outgoing walking-network neighbors. This is an intersection proxy.
- **Edges (`E`):** OpenStreetMap walking segments weighted by their length in meters. The loader treats the graph as undirected and collapses parallel edges between the same endpoints to the shortest supplied length.
- **Distance:** shortest-path distance along the weighted network, not straight-line distance.
- **Selection:** the greedy set-cover heuristic repeatedly chooses the eligible site that newly covers the most demand vertices. Ties are resolved by the lower OpenStreetMap node ID. It stops when all vertices are covered or no remaining site can add coverage.

The optimization tries to cover every demand vertex within the selected radius, but the greedy heuristic does not guarantee a minimum-size solution. If vertices remain uncovered, the application reports an infeasible result for that radius and lists the uncovered count.

Coverage is covered demand vertices divided by all demand vertices. Candidate reduction is unselected eligible sites divided by all eligible sites. Neither metric measures population coverage, confirms that a location is a real transit stop, or verifies pedestrian accessibility.

## Application features

- **Coverage mode:** choose a radius from 50 to 500 meters (500 meters initially), run the heuristic, and review the selected sites, covered and uncovered vertices, coverage, candidate reduction, and elapsed time.
- **Decision replay:** inspect the greedy choices in order using play/pause, start/end, previous/next, the slider, or a decision-history entry. Each step shows its incremental gain and cumulative coverage. The solved radius remains associated with the result even if the radius control changes.
- **Interactive map:** view eligible sites, selected sites, and coverage state. Replay updates retain the current map view. Motion preferences can suppress decorative marker animation.
- **ATM mode:** choose an origin on the map and view up to ten nearby ATMs, ranked by walking-network distance. The origin and ATM are each approximated by their nearest graph vertex, so the distance is a graph-based estimate rather than a route to the exact point.
- **Offline use:** the supplied graph, optimization, replay, and local Leaflet map assets work without internet. OpenStreetMap basemap tiles and graph/ATM updates need internet access.

The checked-in graph currently has no ATM records. Use **Actualizar datos OSM** in ATM mode to retrieve current ATM features and refresh the graph.

## System requirements

### To run the desktop application

- Windows with support for WPF and the **.NET 10 Desktop Runtime**.
- **Microsoft Edge WebView2 Runtime** for the embedded interactive map. If WebView2 is unavailable, the optimization can still run, but the map will not initialize.
- Internet access is optional for coverage analysis with the supplied graph. It is needed for OpenStreetMap basemap tiles.

### To build from source

- **.NET 10 SDK** on Windows.
- Internet access during the first build may be needed to restore the `Microsoft.Web.WebView2` NuGet package.

### To generate or refresh graph data

- **Python 3**, with `osmnx` and `pandas` installed in the same Python environment.
- Internet access to retrieve the walking network and ATM features from OpenStreetMap.
- The application checks `TRANSMART_PYTHON` first, then `py -3`, `python`, and `python3`. Set `TRANSMART_PYTHON` to the Python executable if it is not discoverable through those options.

Graph generation is not required to use the checked-in graph for coverage optimization.

## Run the application

From the repository root, run:

```powershell
dotnet run --project TranSmart/TranSmart.csproj
```

The app copies the bundled `TranSmart/amb_graph.json` to `%LOCALAPPDATA%\TranSmart\amb_graph.json` on first launch, then reads the app-data copy on later launches. If the graph is missing or invalid, use **Generate graph** when prompted. This downloads fresh graph and ATM data and loads it automatically.

## Basic usage

### Analyze network coverage

1. Start the application and select **Optimizar cobertura**.
2. Set the walking radius between 50 and 500 meters.
3. Select **Ejecutar optimización**.
4. Review the summary cards and map. If the result is infeasible, some demand vertices cannot be reached from any eligible site within that radius.
5. Use the replay controls or select an entry in the decision history to inspect how the greedy solution was built.

### Find nearby ATMs

1. Select **Buscar cajeros**.
2. If the graph has no ATM data, choose **Actualizar datos OSM** and wait for the download to finish.
3. Select **Elegir punto en el mapa**, then click your origin on the map.
4. Review the ranked ATM list; select an entry to highlight it on the map.

## Graph data format

The application accepts JSON with `schemaVersion: 2` and `networkType: "walk"`. It requires `nodes` and `edges` arrays; each node has `id`, `lat`, `lon`, and `isCandidateSite`, and each edge has `source`, `target`, and `length`. The optional `atms` array contains `id`, `name`, `lat`, and `lon` records. Older or driving-network graph files must be regenerated.

The app's graph generator runs `TranSmart/scraper.py`. It downloads the simplified walking network and OpenStreetMap features tagged `amenity=atm`, then writes the version 2 graph JSON. The separate repository-root `scraper.py` is an older graph-only script; use the app's **Generate graph** or **Actualizar datos OSM** action to produce data for the current application.

## Project layout

- `TranSmart/TranSmart.csproj` — Windows WPF application project.
- `TranSmart/Models/` — graph and coverage result models.
- `TranSmart/Services/` — graph loading/generation, greedy coverage, and ATM search.
- `TranSmart/ViewModels/` — dashboard state and commands.
- `TranSmart/Map/` — local Leaflet assets and map page.
- `TranSmart/Tests/` — C# and Python tests.
- `TranSmart/amb_graph.json` — bundled sample graph.

## Technology

- C# and .NET 10 WPF
- Microsoft WebView2 and bundled Leaflet.js
- Python 3, OSMnx, pandas, and OpenStreetMap data extraction

## Data and interpretation limits

Graph vertices are not a population sample, and OSM data may be incomplete or outdated. The candidate-site rule is only a reproducible graph-degree proxy. ATM walking distances are estimates between snapped graph vertices. The analysis does not validate transit service, sidewalk condition, access restrictions, safety, or population demand. OpenStreetMap map data is subject to its [copyright and attribution requirements](https://www.openstreetmap.org/copyright).

For the Spanish-language guide, see [README.es.md](README.es.md).
