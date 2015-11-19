namespace CGAL_StraightSkeleton_Dotnet
{
    public class Edge
    {
        public Vertex Start { get; private set; }
        public Vertex End { get; private set; }

        public EdgeType Type { get; private set; }

        public Edge(Vertex start, Vertex end, EdgeType type)
        {
            Start = start;
            End = end;
            Type = type;

            Start.Add(this);
            End.Add(this);
        }

        internal static Edge Create(Vertex start, Vertex end, EdgeType type)
        {
            return new Edge(start, end, type);
        }
    }

    public enum EdgeType
    {
        /// <summary>
        /// Edge is part of the straight edge skeleton
        /// </summary>
        Skeleton,

        /// <summary>
        /// Edge is a spoke connecting the straight edge skeleton out to the border of the shape
        /// </summary>
        Spoke,

        /// <summary>
        /// Edge is part of the outer border of this shape
        /// </summary>
        Border
    }
}
