﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Commons.Mathematics
{
    public class ShortestPathLookup<TVertex, TEdge>
    {
        public IVertex<TVertex> Source { get; }
        private readonly IGraph<TVertex, TEdge> graph;
        private readonly Dictionary<uint, uint> backtraceMap;
        private readonly Dictionary<uint, double> pathLengths;

        public ShortestPathLookup(IGraph<TVertex, TEdge> graph, IVertex<TVertex> source, 
            Dictionary<uint, uint> backtraceMap,
            Dictionary<uint, double> pathLengths)
        {
            Source = source;
            this.graph = graph;
            this.backtraceMap = backtraceMap;
            this.pathLengths = pathLengths;
        }

        public GraphPath<TVertex, TEdge> PathTo(IVertex<TVertex> target)
        {
            if(target.Id == Source.Id)
                return new GraphPath<TVertex, TEdge>(Source.Id);
            if (!backtraceMap.ContainsKey(target.Id))
                throw new ArgumentException("Target is not in graph");

            var path = new List<IEdge<TEdge>>();
            var currentVertexId = target.Id;
            while (currentVertexId != Source.Id)
            {
                var nextVertexId = backtraceMap[currentVertexId];
                var shortestEdge = graph.Vertices[currentVertexId].EdgeIds
                    .Select(edgeId => graph.Edges[edgeId])
                    .Where(edge => edge.HasVertex(nextVertexId))
                    .OrderBy(edge => edge.Weight)
                    .First();
                path.Add(shortestEdge);
                currentVertexId = nextVertexId;
            }
            path.Reverse();

            return new GraphPath<TVertex, TEdge>(Source.Id, path);
        }

        public double PathLengthTo(IVertex<TVertex> target)
        {
            return !pathLengths.ContainsKey(target.Id) ? double.PositiveInfinity : pathLengths[target.Id];
        }
    }
}