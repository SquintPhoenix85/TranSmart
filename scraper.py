import osmnx as ox
import json

# Bounding Box Reducido: Corredor Universitario - Norte de Barranquilla
# OSMnx 2.x expects (west, south, east, north).
west, south, east, north = -74.835, 10.995, -74.810, 11.025

print("Descargando red vial reducida de OSM...")
G = ox.graph_from_bbox(bbox=(west, south, east, north), network_type='drive', simplify=True)

# Estructurar datos para exportación a JSON
nodes_data = []
for node_id, data in G.nodes(data=True):
    nodes_data.append({
        "id": int(node_id),
        "lat": data['y'],
        "lon": data['x']
    })

edges_data = []
for u, v, data in G.edges(data=True):
    edges_data.append({
        "source": int(u),
        "target": int(v),
        "length": round(data.get('length', 0), 2)  # Distancia en metros
    })

graph_json = {
    "nodes": nodes_data,
    "edges": edges_data
}

with open("amb_graph.json", "w", encoding="utf-8") as f:
    json.dump(graph_json, f, indent=4)

print(f"Éxito: Grafo ligero exportado con {len(nodes_data)} nodos y {len(edges_data)} aristas.")
