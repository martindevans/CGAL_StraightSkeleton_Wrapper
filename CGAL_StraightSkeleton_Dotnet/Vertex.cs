using System;
using System.Collections.Generic;
using System.Numerics;

namespace CGAL_StraightSkeleton_Dotnet
{
    public class Vertex
    {
        public Vector2 Position { get; set; }

        private readonly HashSet<Edge> _edges = new HashSet<Edge>(); 
        public IEnumerable<Edge> Edges { get { return _edges; } }

        public Vertex(Vector2 position)
        {
            Position = position;
        }

        public void Add(Edge e)
        {
            if (e.Start != this && e.End != this)
                throw new ArgumentException("Edge connecting to vertex must start or end with vertex", "e");

            _edges.Add(e);
        }
    }
}
