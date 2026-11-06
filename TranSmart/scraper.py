import argparse
import json
import os
import sys
import tempfile

# Bounding Box Reducido: Corredor Universitario - Norte de Barranquilla
# OSMnx 2.x expects (west, south, east, north).
west, south, east, north = -74.835, 10.995, -74.810, 11.025


def main():
    parser = argparse.ArgumentParser(description="Generate the Barranquilla road graph JSON.")
    parser.add_argument("--output", default="amb_graph.json", help="Output path for the generated graph JSON.")
    args = parser.parse_args()

    try:
        import osmnx as ox
    except ImportError:
        print("OSMnx is not installed. Install it with 'python -m pip install osmnx' and try again.", file=sys.stderr)
        return 2

    temporary_path = None
    try:
        print("Downloading the road network from OpenStreetMap...", flush=True)
        graph = ox.graph_from_bbox(bbox=(west, south, east, north), network_type="drive", simplify=True)

        nodes_data = []
        for node_id, data in graph.nodes(data=True):
            nodes_data.append({
                "id": int(node_id),
                "lat": data["y"],
                "lon": data["x"]
            })

        edges_data = []
        for source, target, data in graph.edges(data=True):
            edges_data.append({
                "source": int(source),
                "target": int(target),
                "length": round(data.get("length", 0), 2)
            })

        graph_json = {"nodes": nodes_data, "edges": edges_data}
        output_path = os.path.abspath(args.output)
        output_directory = os.path.dirname(output_path)
        os.makedirs(output_directory, exist_ok=True)
        print(f"Writing {len(nodes_data)} vertices and {len(edges_data)} edges...", flush=True)

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
