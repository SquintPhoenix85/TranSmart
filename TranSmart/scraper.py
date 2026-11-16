import argparse
import json
import os
import sys
import tempfile

# Bounding Box Reducido: Corredor Universitario - Norte de Barranquilla
# OSMnx 2.x expects (west, south, east, north).
west, south, east, north = -74.835, 10.995, -74.810, 11.025


def main():
    parser = argparse.ArgumentParser(description="Genera el archivo JSON del grafo vial de Barranquilla.")
    parser.add_argument("--output", default="amb_graph.json", help="Ruta de salida del archivo JSON del grafo.")
    args = parser.parse_args()

    try:
        import osmnx as ox
        import pandas as pd
    except ImportError:
        print("OSMnx is not installed. Install it with 'python -m pip install osmnx' and try again.", file=sys.stderr)
        return 2

    temporary_path = None
    try:
        print("Downloading the road network from OpenStreetMap...", flush=True)
        graph = ox.graph_from_bbox(bbox=(west, south, east, north), network_type="walk", simplify=True)

        print("Downloading ATMs from OpenStreetMap...", flush=True)
        atm_features = ox.features_from_bbox(bbox=(west, south, east, north), tags={"amenity": "atm"})
        atms_data = []
        for osm_id, row in atm_features.iterrows():
            geometry = row.geometry
            if geometry is None or geometry.is_empty:
                continue
            point = geometry if geometry.geom_type == "Point" else geometry.representative_point()
            name = row.get("name")
            if pd.isna(name) or not str(name).strip() or str(name).strip().casefold() in {"nan", "none", "null", "<na>"}:
                name = "Cajero automático sin nombre"
            else:
                name = str(name).strip()
            atms_data.append({
                "id": str(osm_id),
                "name": name,
                "lat": float(point.y),
                "lon": float(point.x),
            })

        nodes_data = []
        for node_id, data in graph.nodes(data=True):
            nodes_data.append({
                "id": int(node_id),
                "lat": data["y"],
                "lon": data["x"],
                "isCandidateSite": len((set(graph.predecessors(node_id)) | set(graph.successors(node_id))) - {node_id}) >= 3
            })

        edges_data = []
        for source, target, data in graph.edges(data=True):
            edges_data.append({
                "source": int(source),
                "target": int(target),
                "length": data["length"]
            })

        graph_json = {"schemaVersion": 2, "networkType": "walk", "nodes": nodes_data, "edges": edges_data, "atms": atms_data}
        output_path = os.path.abspath(args.output)
        output_directory = os.path.dirname(output_path)
        os.makedirs(output_directory, exist_ok=True)
        print(f"Writing {len(nodes_data)} vertices, {len(edges_data)} edges, and {len(atms_data)} ATMs...", flush=True)

        with tempfile.NamedTemporaryFile(
            mode="w", encoding="utf-8", dir=output_directory, delete=False
        ) as output_file:
            temporary_path = output_file.name
            json.dump(graph_json, output_file, indent=4)

        os.replace(temporary_path, output_path)
        temporary_path = None
        print(f"Graph generated successfully: {output_path}", flush=True)
        return 0
    except Exception as error:
        print(f"Graph generation failed: {error}", file=sys.stderr, flush=True)
        return 1
    finally:
        if temporary_path is not None and os.path.exists(temporary_path):
            os.remove(temporary_path)


if __name__ == "__main__":
    sys.exit(main())
