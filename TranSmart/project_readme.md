# Optimization of Transit Infrastructure & Stop Coverage (AMB - North Corridor)

**Course:** Discrete Mathematics (MAT 4021) — Universidad del Norte  
**Project Objective:** Solve the Set Cover Problem ($V' \subseteq V$) over a real-world transport graph in the Northern Corridor of Barranquilla using C# (.NET / WPF) and Python (`OSMnx`).

---

## 1. Context & Impact Definition

* **Problem Statement:** Inefficient location of public transit access, recharge, or transfer points along high-density corridors in the Barranquilla Metropolitan Area (AMB).
* **Target Population:** University students (Uninorte community) and commuters along the Northern Corridor (Cra 51B / Cra 53 / Calle 84).
* **Measurable Impact:**
  * Reduction in total candidate transit/recharge nodes ($|V'|$) required to serve the area.
  * Percentage of graph vertices covered within a walking-network radius ($R \le 500\text{ m}$).
  * Algorithmic runtime efficiency ($ms$) and spatial coverage comparison.

---

## 2. Geofenced Scope & Bounding Box

To maintain computational efficiency and high data density, the project isolates the **Uninorte – Northern Corridor** area rather than the entire municipality.

* **North (Lat):** $11.025^\circ \text{N}$ (Villa Campestre / Uninorte)
* **South (Lat):** $10.995^\circ \text{N}$ (Calle 84 / Commercial Area)
* **West (Lon):** $-74.835^\circ \text{W}$ (Vía al Mar / Cra 53)
* **East (Lon):** $-74.810^\circ \text{W}$ (Alto Prado / Cra 46)

---

## 3. Mathematical Modeling (Discrete Theory)

The urban road system is modeled as a weighted undirected graph $G = (V, E)$:
* **Vertices ($V$):** Intersections and candidate bus stops/stations.
* **Edges ($E$):** Road segments with weights $w(e)$ corresponding to physical distance in meters.
* **Optimization Goal (Greedy Set Cover):**
  $$\text{Minimize } |V'| \quad \text{subject to } \bigcup_{v \in V'} N_R(v) = V$$
  where $N_R(v) = \{ u \in V \mid d(u, v) \le R \}$ represents the neighborhood of nodes within walking distance $R$.

---

## 4. Software Architecture & Technology Stack

* **Data Ingestion & Extraction:** Python (`OSMnx`, `NetworkX`)
  * Downloads OpenStreetMap data within the defined Bounding Box.
  * Simplifies the graph topology and exports `amb_graph.json` through `scraper.py`.
* **Core Optimization Engine & Business Logic:** C# (.NET)
  * Deserializes `amb_graph.json`.
  * Computes shortest path distance matrices using Dijkstra's algorithm.
  * Executes the Greedy Set Cover heuristic to find optimal node placement.
* **Frontend / User Interface:** C# (WPF) + `WebView2`
  * Interactive control panel (Sliders for radius $R$, action buttons, execution metrics).
  * Map rendering via `WebView2` with Leaflet.js markers for selected nodes.
* **Documentation:** LaTeX (APA style).

---

## 5. System Data Pipeline

```
[OpenStreetMap API]
       │
       ▼
     [scraper.py] ──(Python / OSMnx)
       │
       ▼
 [amb_graph.json]
       │
       ▼
 [AMBCoverageOptimizer] ──(C# .NET Engine)
       │
       ├── Greedy Set Cover Algorithm
       ├── Dijkstra Distance Matrix
       └── WPF Dashboard & WebView2 Leaflet Map
```

---

## 6. Project Objectives Summary

1. **Model:** Extract and represent the Northern Corridor graph ($G=(V,E)$) from real OSM spatial data.
2. **Algorithm:** Apply the greedy Set Cover heuristic in C# to choose candidate vertices for variable radii $R$; the result is not guaranteed to be a minimum cover.
3. **Evaluation:** Analyze efficiency, node reduction percentages, and performance over sample test cases.
4. **Deliverables:**
   * LaTeX Final Report (30%)
   * Executable C# WPF Application + Python Extractor (30%)
   * Oral Defense & Presentation (40%)

---

## 7. Dashboard & Decision Replay

The WPF dashboard uses the built-in Fluent dark theme. Run an optimization to see the final graph-vertex metrics and an ordered replay of the greedy choices. Use the play/pause, start/end, previous/next, slider, or decision-history controls to inspect a particular step. The replay starts with no vertices covered and advances one selected site at a time; each history entry reports that site's incremental gain and cumulative covered-vertex count. The solved radius is shown separately from the radius slider so an existing result remains clearly labeled if the slider is changed afterward.

On the map, blue marks selected sites, orange marks vertices first covered by the current step, green marks vertices covered in earlier steps, and gray marks vertices not covered yet. Marker transitions animate during playback when motion is enabled; reduced-motion settings suppress the decorative animation. The map retains its pan and zoom while stepping through a result.

The optimization, decision history, and map markers work without internet access. OpenStreetMap basemap tiles require an internet connection and may be unavailable offline. Results describe coverage of vertices in the supplied road graph only: they do not establish population coverage, identify verified transit stops, prove pedestrian accessibility, or guarantee an optimal (minimum-size) set cover.

If `amb_graph.json` is missing or cannot be loaded, the dashboard offers a **Generate graph** action. It runs the bundled `scraper.py`, writes the graph into the application's data location, and loads it automatically. Graph generation requires Python 3 with OSMnx installed (`python -m pip install osmnx`) and an internet connection to retrieve OpenStreetMap data. The app searches for `py -3`, `python`, and `python3`; set the `TRANSMART_PYTHON` environment variable to a Python executable when it is installed outside `PATH`.