import json
import os
from pathlib import Path
import runpy
import sys
import tempfile
import unittest
from unittest.mock import patch

import networkx as nx


APP = Path(__file__).resolve().parents[1]
SCRIPTS = (APP / "scraper.py", APP.parent / "scraper.py")


class ScraperTests(unittest.TestCase):
    def test_regenerated_graphs(self):
        for path in (APP / "amb_graph.json", APP.parent / "amb_graph.json"):
            with self.subTest(path=path), path.open(encoding="utf-8") as file:
                graph = json.load(file)
                self.assertEqual(graph["schemaVersion"], 2)
                self.assertEqual(graph["networkType"], "walk")
                neighbors = {node["id"]: set() for node in graph["nodes"]}
                for edge in graph["edges"]:
                    source, target = edge["source"], edge["target"]
                    if source != target:
                        neighbors[source].add(target)
                        neighbors[target].add(source)
                for node in graph["nodes"]:
                    self.assertEqual(node["isCandidateSite"], len(neighbors[node["id"]]) >= 3)
                print(f"{path}: {len(graph['nodes'])} nodes, {len(graph['edges'])} edges, "
                      f"{sum(node['isCandidateSite'] for node in graph['nodes'])} candidates")

    def test_walking_graph_and_distinct_neighbor_candidates(self):
        graph = nx.MultiDiGraph()
        for node in range(1, 6):
            graph.add_node(node, y=10 + node, x=-70 - node)
        graph.add_edge(2, 1, length=1.234)
        graph.add_edge(1, 2, length=2)
        graph.add_edge(1, 2, length=3)
        graph.add_edge(3, 1, length=4)
        graph.add_edge(1, 4, length=5)
        graph.add_edge(5, 5, length=6)

        for script in SCRIPTS:
            with self.subTest(script=script), tempfile.TemporaryDirectory() as directory:
                calls = []
                def graph_from_bbox(**kwargs):
                    calls.append(kwargs)
                    return graph

                import types
                fake_osmnx = types.SimpleNamespace(graph_from_bbox=graph_from_bbox)
                previous = os.getcwd()
                try:
                    os.chdir(directory)
                    with patch.dict(sys.modules, {"osmnx": fake_osmnx}), patch.object(sys, "argv", [str(script)]):
                        try:
                            runpy.run_path(str(script), run_name="__main__")
                        except SystemExit as error:
                            self.assertEqual(error.code, 0)
                    with open("amb_graph.json", encoding="utf-8") as file:
                        result = json.load(file)
                finally:
                    os.chdir(previous)

                self.assertEqual(calls[0]["network_type"], "walk")
                self.assertEqual(result["schemaVersion"], 2)
                self.assertEqual(result["networkType"], "walk")
                self.assertEqual([node["isCandidateSite"] for node in result["nodes"]],
                                 [True, False, False, False, False])
                self.assertEqual(len(result["edges"]), 6)
                self.assertEqual(result["nodes"][0]["lat"], 11)
                self.assertEqual(result["edges"][0]["length"], 2)
                self.assertEqual(next(edge["length"] for edge in result["edges"]
                                      if edge["source"] == 2 and edge["target"] == 1), 1.234)

    def test_missing_length_is_not_exported_as_zero(self):
        graph = nx.MultiDiGraph()
        graph.add_node(1, y=1, x=1)
        graph.add_node(2, y=2, x=2)
        graph.add_edge(1, 2)
        import types
        for script in SCRIPTS:
            with self.subTest(script=script), tempfile.TemporaryDirectory() as directory:
                previous = os.getcwd()
                try:
                    os.chdir(directory)
                    with patch.dict(sys.modules, {"osmnx": types.SimpleNamespace(graph_from_bbox=lambda **kwargs: graph)}), \
                         patch.object(sys, "argv", [str(script)]):
                        if script == APP / "scraper.py":
                            with self.assertRaises(SystemExit) as error:
                                runpy.run_path(str(script), run_name="__main__")
                            self.assertEqual(error.exception.code, 1)
                        else:
                            with self.assertRaises(KeyError):
                                runpy.run_path(str(script), run_name="__main__")
                    self.assertFalse(Path("amb_graph.json").exists())
                finally:
                    os.chdir(previous)


if __name__ == "__main__":
    unittest.main()