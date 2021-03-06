﻿using System;
using System.Collections.Generic;
using System.Linq;
using Commons.Extensions;

namespace Commons.Mathematics
{
    public static class GraphAlgorithms
    {
        public static double[,] ComputeAdjacencyMatrix(IGraph graph)
        {
            var vertexCount = graph.Vertices.Count();
            var adjacencyMatrix = new double[vertexCount, vertexCount];

            var sortedVertexIds = graph.Vertices.Select(v => v.Id).OrderBy(x => x).ToList();
            foreach (var edge in graph.Edges)
            {
                var vertex1Index = sortedVertexIds.IndexOf(edge.Vertex1Id);
                var vertex2Index = sortedVertexIds.IndexOf(edge.Vertex2Id);
                adjacencyMatrix[vertex1Index, vertex2Index]++;
                adjacencyMatrix[vertex2Index, vertex1Index]++;
            }

            return adjacencyMatrix;
        }

        public static ShortestPathLookup ShortestPaths(IGraph graph, uint sourceVertexId)
        {
            return ShortestPaths(graph, graph.GetVertexFromId(sourceVertexId));
        }
        public static ShortestPathLookup ShortestPaths(IGraph graph, IVertex source)
        {
            if (!graph.HasVertex(source.Id))
                throw new ArgumentException("GraphAlgorithms.ShortestPath: Source vertex not in graph");
            if (graph.Edges.Any(e => e.Weight < 0))
                throw new NotImplementedException("GraphAlgorithms.ShortestPath: No shortest path algorithm is implemented for graphs with negative edge-weights");

            // Dijkstra's algorithm
            var unVisitedVertexDictionary = graph.Vertices.ToDictionary(v => v.Id, v => true);
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
                var currentVertex = graph.GetVertexFromId(unvisitedWithShortestPathLength);

                unVisitedVertexDictionary[currentVertex.Id] = false;
                unvisitedVertexCount--;

                // Update adjacent vertices
                var adjacentEdgeVertexDictionary = currentVertex.EdgeIds
                    .Select(graph.GetEdgeById)
                    .Where(e => !e.IsDirected || e.Vertex1Id == currentVertex.Id)
                    .Select(e => new { VertexId = e.Vertex1Id != currentVertex.Id ? e.Vertex1Id : e.Vertex2Id, Edge = e });
                var currentVertexPathLength = shortestPathLengths[currentVertex.Id];
                foreach (var vertexEdgePair in adjacentEdgeVertexDictionary)
                {
                    var adjacentVertex = graph.GetVertexFromId(vertexEdgePair.VertexId);
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
            return new ShortestPathLookup(graph,
                source,
                backtraceMap,
                shortestPathLengths);
        }

        public static bool IsGraphConnected<TVertex, TEdge>(Graph<TVertex, TEdge> graph)
        {
            if (!graph.Vertices.Any())
                return true;
            var startVertex = graph.Vertices.First();
            var connectedVertices = GetConnectedSubgraph(graph, startVertex).Vertices;
            return graph.Vertices.Count() == connectedVertices.Count();
        }

        public static void ApplyMethodToAllConnectedVertices<TVertex, TEdge>(Graph<TVertex, TEdge> graph, IVertex startVertex, Action<IVertex> action)
        {
            foreach (var connectedVertex in GetConnectedSubgraph(graph, startVertex).Vertices)
            {
                action(connectedVertex);
            }
        }

        public static IGraph<TVertex, TEdge> GetConnectedSubgraph<TVertex, TEdge>(IGraph<TVertex, TEdge> graph, IVertex startVertex)
        {
            // Use depth first search for traversing connected component
            // Initialize graph algorithm data
            graph.Vertices.ForEach(v => v.AlgorithmData = false);
            graph.Edges.ForEach(e => e.AlgorithmData = false);

            var connectedVertices = GetConnectedVertices(graph, startVertex).Select(v => v.Id).ToList();
            return GetSubgraph(graph, connectedVertices);
        }

        private static IEnumerable<IVertex> GetConnectedVertices<TVertex, TEdge>(IGraph<TVertex, TEdge> graph, IVertex currentVertex)
        {
            // Mark vertex as visited
            currentVertex.AlgorithmData = true;

            var unvisitedAdjacentVertices = currentVertex.EdgeIds
                .Select(graph.GetEdgeById)
                .Select(edge => edge.Vertex1Id == currentVertex.Id ? edge.Vertex2Id : edge.Vertex1Id);

            foreach (var adjacentVertexId in unvisitedAdjacentVertices)
            {
                var adjacentVertex = graph.GetVertexFromId(adjacentVertexId);
                if (adjacentVertex.AlgorithmData.Equals(true))
                    continue;

                foreach (var vertex in GetConnectedVertices(graph, adjacentVertex))
                {
                    yield return vertex;
                }
            }
            yield return currentVertex;
        }

        public static IEnumerable<IVertex> GetAdjacentVertices(IGraph graph, IVertex vertex)
        {
            return vertex.EdgeIds
                .Select(graph.GetEdgeById)
                .Where(edge => !edge.IsDirected || edge.Vertex1Id == vertex.Id)
                .Select(edge => edge.Vertex1Id == vertex.Id ? edge.Vertex2Id : edge.Vertex1Id)
                .Distinct()
                .Select(graph.GetVertexFromId);
        }

        public static IGraph<TVertex, TEdge> GetSubgraph<TVertex, TEdge>(IGraph<TVertex, TEdge> graph, IList<uint> vertices)
        {
            var subgraphVertices = vertices
                .Distinct()
                .Select(vertexId => ((ICloneable)graph.GetVertexFromId(vertexId)).Clone() as IVertex<TVertex>)
                .ToList();
            subgraphVertices.ForEach(v => v.EdgeIds.Clear());
            var vertexIdHashSet = new HashSet<uint>(vertices);
            var subgraphEdges = graph.Edges
                .Where(e => vertexIdHashSet.Contains(e.Vertex1Id) && vertexIdHashSet.Contains(e.Vertex2Id));
            var subgraph = new Graph<TVertex, TEdge>(subgraphVertices, subgraphEdges);
            foreach (var vertex in subgraph.Vertices)
            {
                vertex.EdgeIds.RemoveAll(edgeId => !subgraph.HasEdge(edgeId));
            }
            return subgraph;
        }

        public static bool HasCycles(IGraph graph, bool treatAllEdgesAsUndirected = false)
        {
            var isFullyDirected = graph.Edges.All(e => e.IsDirected);
            if (isFullyDirected)
            {
                var stronglyConnectedComponents = FindStronglyConnectedComponents(graph);
                return stronglyConnectedComponents.Count != graph.Vertices.Count();
            }
            var isFullyUndirected = graph.Edges.All(e => !e.IsDirected);
            if (isFullyUndirected || treatAllEdgesAsUndirected)
            {
                graph.Vertices.ForEach(v => v.AlgorithmData = false);
                graph.Edges.ForEach(v => v.AlgorithmData = false);
                var edgeStack = new Stack<IEdge>();
                while (edgeStack.Any() || graph.Vertices.Any(e => (bool)e.AlgorithmData == false))
                {
                    if (!edgeStack.Any())
                    {
                        var unvisitedVertex = graph.Vertices.First(v => (bool)v.AlgorithmData == false);
                        unvisitedVertex.AlgorithmData = true;
                        unvisitedVertex.EdgeIds.Select(graph.GetEdgeById).ForEach(edgeStack.Push);
                    }
                    var edge = edgeStack.Pop();
                    edge.AlgorithmData = true;
                    var vertex = graph.GetVertexFromId(edge.Vertex1Id);
                    var wasAlreadyVisited = (bool) vertex.AlgorithmData;
                    if (wasAlreadyVisited)
                    {
                        vertex = graph.GetVertexFromId(edge.Vertex2Id);
                        wasAlreadyVisited = (bool) vertex.AlgorithmData;
                        if (wasAlreadyVisited) // If both ends of an edge have already been visited, we have a cycle
                            return true;
                    }
                    vertex.AlgorithmData = true;
                    vertex.EdgeIds.Select(graph.GetEdgeById)
                        //.Where(e => !e.IsDirected || e.Vertex1Id == vertex.Id) // treatAllEdgesAsUndirected
                        .Where(e => (bool)e.AlgorithmData == false)
                        .ForEach(edgeStack.Push);
                }
                return false;
            }
            else
            {
                throw new NotImplementedException("Finding cycles is only supported for graphs with all edges being directed or all being undirected");
            }
        }

        public static List<StronglyConnectedComponent> FindStronglyConnectedComponents(IGraph graph)
        {
            graph.Vertices.ForEach(v => v.AlgorithmData = new TarjanStrongConnectedComponentAlgorithmData());
            var index = 0u;
            var vertexStack = new Stack<IVertex>();
            var strongConnectedComponents = new List<StronglyConnectedComponent>();
            foreach (var vertex in graph.Vertices)
            {
                var algorithmData = (TarjanStrongConnectedComponentAlgorithmData) vertex.AlgorithmData;
                if (!algorithmData.Index.HasValue)
                {
                    StrongConnect(vertex, graph, vertexStack, strongConnectedComponents, ref index);
                }
            }

            return strongConnectedComponents;
        }

        private static void StrongConnect(IVertex vertex, 
            IGraph graph, 
            Stack<IVertex> vertexStack,
            List<StronglyConnectedComponent> strongConnectedComponents,
            ref uint index)
        {
            var algorithmData = (TarjanStrongConnectedComponentAlgorithmData) vertex.AlgorithmData;
            algorithmData.Index = index;
            algorithmData.LowLink = index;
            index++;
            vertexStack.Push(vertex);
            algorithmData.IsOnStack = true;

            foreach (var adjacentVertex in GetAdjacentVertices(graph, vertex))
            {
                var adjacentAlgorithmData = (TarjanStrongConnectedComponentAlgorithmData) adjacentVertex.AlgorithmData;
                if (!adjacentAlgorithmData.Index.HasValue)
                {
                    StrongConnect(adjacentVertex, graph, vertexStack, strongConnectedComponents, ref index);
                    algorithmData.LowLink = Math.Min(algorithmData.LowLink.Value, adjacentAlgorithmData.LowLink.Value);
                }
                else if(adjacentAlgorithmData.IsOnStack)
                {
                    algorithmData.LowLink = Math.Min(algorithmData.LowLink.Value, adjacentAlgorithmData.Index.Value);
                }
            }

            if (algorithmData.LowLink == algorithmData.Index)
            {
                IVertex stackVertex;
                var strongConnectedComponent = new StronglyConnectedComponent();
                do
                {
                    stackVertex = vertexStack.Pop();
                    var stackVertexAlgorithmData = (TarjanStrongConnectedComponentAlgorithmData)stackVertex.AlgorithmData;
                    stackVertexAlgorithmData.IsOnStack = false;
                    strongConnectedComponent.Add(stackVertex);
                } while (!stackVertex.Equals(vertex));
                strongConnectedComponents.Add(strongConnectedComponent);
            }
        }

        private class TarjanStrongConnectedComponentAlgorithmData
        {
            public bool IsOnStack { get; set; }
            public uint? Index { get; set; }
            public uint? LowLink { get; set; }
        }
    }
}