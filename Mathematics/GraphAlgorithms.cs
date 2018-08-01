﻿using System;
using System.Collections.Generic;
using System.Linq;
using Commons.Extensions;

namespace Commons.Mathematics
{
    public static class GraphAlgorithms
    {
        public static double[,] ComputeAdjacencyMatrix<TVertex, TEdge>(Graph<TVertex, TEdge> graph)
        {
            var adjacencyMatrix = new double[graph.Vertices.Count, graph.Vertices.Count];

            var sortedVertexIds = graph.Vertices.Values.Select(v => v.Id).OrderBy(x => x).ToList();
            foreach (var edge in graph.Edges.Values)
            {
                var vertex1Index = sortedVertexIds.IndexOf(edge.Vertex1Id);
                var vertex2Index = sortedVertexIds.IndexOf(edge.Vertex2Id);
                adjacencyMatrix[vertex1Index, vertex2Index]++;
                adjacencyMatrix[vertex2Index, vertex1Index]++;
            }

            return adjacencyMatrix;
        }

        public static ShortestPathLookup<TVertex, TEdge> ShortestPaths<TVertex, TEdge>(IGraph<TVertex,TEdge> graph, uint sourceVertexId)
        {
            return ShortestPaths(graph, graph.Vertices[sourceVertexId]);
        }
        public static ShortestPathLookup<TVertex, TEdge> ShortestPaths<TVertex, TEdge>(IGraph<TVertex, TEdge> graph, IVertex<TVertex> source)
        {
            if (!graph.Vertices.ContainsKey(source.Id))
                throw new ArgumentException("GraphAlgorithms.ShortestPath: Source vertex not in graph");
            if (graph.Edges.Values.Any(e => e.Weight < 0))
                throw new NotImplementedException("GraphAlgorithms.ShortestPath: No shortest path algorithm is implemented for graphs with negative edge-weights");

            // Dijkstra's algorithm
            var unVisitedVertexDictionary = graph.Vertices.ToDictionary(v => v.Key, v => true);
            var unvisitedVertexCount = unVisitedVertexDictionary.Count;
            var shortestPathLengths = new Dictionary<uint, double> { { source.Id, 0} };
            var backtraceMap = new Dictionary<uint, uint>();

            while (unvisitedVertexCount > 0)
            {
                var unvisitedVertices = shortestPathLengths
                    .Where(kvp => unVisitedVertexDictionary[kvp.Key])
                    .ToList();
                // Graph is not connected. Either stop here or throw exception.
                if(!unvisitedVertices.Any())
                    break;//throw new Exception("GraphAlgorithms.ShortestPath: Graph appears to be not connected.");

                var unvisitedWithShortestPathLength = unvisitedVertices
                    .OrderBy(kvp => kvp.Value)
                    .First().Key;
                var currentVertex = graph.Vertices[unvisitedWithShortestPathLength];

                unVisitedVertexDictionary[currentVertex.Id] = false;
                unvisitedVertexCount--;

                // Update adjacent vertices
                var adjacentEdgeVertexDictionary = currentVertex.EdgeIds
                    .Select(e => graph.Edges[e])
                    .Where(e => !e.IsDirected || e.Vertex1Id == currentVertex.Id)
                    .Select(e => new { VertexId = e.Vertex1Id != currentVertex.Id ? e.Vertex1Id : e.Vertex2Id, Edge = e });
                var currentVertexPathLength = shortestPathLengths[currentVertex.Id];
                foreach (var vertexEdgePair in adjacentEdgeVertexDictionary)
                {
                    var adjacentVertex = graph.Vertices[vertexEdgePair.VertexId];
                    var adjacentEdge = vertexEdgePair.Edge;
                    // Skip already visited vertices
                    if (!unVisitedVertexDictionary[adjacentVertex.Id])
                        continue;
                    var currentShortestPathLength = shortestPathLengths.ContainsKey(adjacentVertex.Id)
                        ? shortestPathLengths[adjacentVertex.Id]
                        : double.PositiveInfinity;
                    if (currentShortestPathLength < currentVertexPathLength + adjacentEdge.Weight)
                        continue;
                    backtraceMap[adjacentVertex.Id] = currentVertex.Id;
                    shortestPathLengths[adjacentVertex.Id] = currentVertexPathLength + adjacentEdge.Weight;
                }
            }
            return new ShortestPathLookup<TVertex, TEdge>(graph,
                source,
                backtraceMap,
                shortestPathLengths);
        }

        public static bool IsGraphConnected<TVertex, TEdge>(Graph<TVertex, TEdge> graph)
        {
            if (!graph.Vertices.Any())
                return true;
            var startVertex = graph.Vertices.Values.First();
            var connectedVertices = GetConnectedSubgraph(graph, startVertex).Vertices;
            return graph.Vertices.Count == connectedVertices.Count;
        }

        public static void ApplyMethodToAllConnectedVertices<TVertex, TEdge>(Graph<TVertex, TEdge> graph, IVertex<TVertex> startVertex, Action<IVertex<TVertex>> action)
        {
            foreach (var connectedVertex in GetConnectedSubgraph(graph, startVertex).Vertices.Values)
            {
                action(connectedVertex);
            }
        }

        public static IGraph<TVertex, TEdge> GetConnectedSubgraph<TVertex, TEdge>(IGraph<TVertex, TEdge> graph, IVertex<TVertex> startVertex)
        {
            // Use depth first search for traversing connected component
            // Initialize graph algorithm data
            graph.Vertices.ForEach(v => v.Value.AlgorithmData = false);
            graph.Edges.Values.ForEach(e => e.AlgorithmData = false);

            var connectedVertices = GetConnectedVertices(graph, startVertex).Select(v => v.Id).ToList();
            return GetSubgraph(graph, connectedVertices);
        }

        private static IEnumerable<IVertex<TVertex>> GetConnectedVertices<TVertex, TEdge>(IGraph<TVertex, TEdge> graph, IVertex<TVertex> currentVertex)
        {
            // Mark vertex as visited
            currentVertex.AlgorithmData = true;

            var unvisitedAdjacentVertices = currentVertex.EdgeIds
                .Select(edgeId => graph.Edges[edgeId])
                .Select(edge => edge.Vertex1Id == currentVertex.Id ? edge.Vertex2Id : edge.Vertex1Id);

            foreach (var adjacentVertexId in unvisitedAdjacentVertices)
            {
                var adjacentVertex = graph.Vertices[adjacentVertexId];
                if (adjacentVertex.AlgorithmData.Equals(true))
                    continue;

                foreach (var vertex in GetConnectedVertices(graph, adjacentVertex))
                {
                    yield return vertex;
                }
            }
            yield return currentVertex;
        }

        public static IEnumerable<IVertex<TVertex>> GetAdjacentVertices<TVertex, TEdge>(IGraph<TVertex, TEdge> graph, IVertex<TVertex> vertex)
        {
            return vertex.EdgeIds
                .Select(edgeId => graph.Edges[edgeId])
                .Select(edge => edge.Vertex1Id == vertex.Id ? edge.Vertex2Id : edge.Vertex1Id)
                .Distinct()
                .Select(vId => graph.Vertices[vId]);
        }

        public static IGraph<TVertex, TEdge> GetSubgraph<TVertex, TEdge>(IGraph<TVertex, TEdge> graph, IList<uint> vertices)
        {
            var subgraphVertices = vertices
                .Distinct()
                .Select(vertexId => ((ICloneable)graph.Vertices[vertexId]).Clone() as IVertex<TVertex>)
                .ToList();
            subgraphVertices.ForEach(v => v.EdgeIds.Clear());
            var vertexIdHashSet = new HashSet<uint>(vertices);
            var subgraphEdges = graph.Edges.Values
                .Where(e => vertexIdHashSet.Contains(e.Vertex1Id) && vertexIdHashSet.Contains(e.Vertex2Id));
            var subgraph = new Graph<TVertex, TEdge>(subgraphVertices, subgraphEdges);
            foreach (var vertex in subgraph.Vertices.Values)
            {
                vertex.EdgeIds.RemoveAll(edgeId => !subgraph.Edges.ContainsKey(edgeId));
            }
            return subgraph;
        }

        public static bool HasCycles<TVertex, TEdge>(IGraph<TVertex, TEdge> graph)
        {
            graph.Vertices.Values.ForEach(v => v.AlgorithmData = false);
            graph.Edges.Values.ForEach(v => v.AlgorithmData = false);
            var edgeStack = new Stack<IEdge<TEdge>>();
            while (edgeStack.Any() || graph.Vertices.Values.Any(e => (bool)e.AlgorithmData == false))
            {
                if (!edgeStack.Any())
                {
                    var unvisitedVertex = graph.Vertices.Values.First(v => (bool)v.AlgorithmData == false);
                    unvisitedVertex.AlgorithmData = true;
                    unvisitedVertex.EdgeIds.Select(edgeId => graph.Edges[edgeId]).ForEach(edgeStack.Push);
                }
                var edge = edgeStack.Pop();
                edge.AlgorithmData = true;
                var vertex = graph.Vertices[edge.Vertex1Id];
                var wasAlreadyVisited = (bool) vertex.AlgorithmData;
                if (wasAlreadyVisited)
                {
                    vertex = graph.Vertices[edge.Vertex2Id];
                    wasAlreadyVisited = (bool) vertex.AlgorithmData;
                    if (wasAlreadyVisited) // If both ends of an edge have already been visited, we have a cycle
                        return true;
                }
                vertex.AlgorithmData = true;
                vertex.EdgeIds.Select(edgeId => graph.Edges[edgeId])
                    .Where(e => !e.IsDirected || e.Vertex1Id == vertex.Id)
                    .Where(e => (bool)e.AlgorithmData == false)
                    .ForEach(edgeStack.Push);
            }
            return false;
        }
    }
}