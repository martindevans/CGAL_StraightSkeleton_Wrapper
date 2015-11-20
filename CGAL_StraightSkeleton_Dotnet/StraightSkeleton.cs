using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace CGAL_StraightSkeleton_Dotnet
{
    public sealed class StraightSkeleton
        : IDisposable
    {
        private readonly IntPtr _opaqueHandle;

        private readonly HashSet<Edge> _borders;
        /// <summary>
        /// Edges around the outside of the shape
        /// </summary>
        public IEnumerable<Edge> Borders
        {
            get { return _borders; }
        }

        private readonly HashSet<Edge> _spokes;
        /// <summary>
        /// Edges connecting outside of shape to the skeleton
        /// </summary>
        public IEnumerable<Edge> Spokes
        {
            get { return _spokes; }
        }

        private readonly HashSet<Edge> _skeleton;
        /// <summary>
        /// The spine of the skeleton
        /// </summary>
        public IEnumerable<Edge> Skeleton
        {
            get { return _skeleton; }
        }

        private StraightSkeleton(HashSet<Vector2> inputVertices, HashSet<KeyValuePair<Vector2, Vector2>> borders, HashSet<KeyValuePair<Vector2, Vector2>> spokes, HashSet<KeyValuePair<Vector2, Vector2>> skeleton, IntPtr opaqueHandle)
        {
            _opaqueHandle = opaqueHandle;

            Initialize(borders, spokes, skeleton, out _borders, out _spokes, out _skeleton);
        }

        private static void Initialize(IEnumerable<KeyValuePair<Vector2, Vector2>> borders, IEnumerable<KeyValuePair<Vector2, Vector2>> spokes, IEnumerable<KeyValuePair<Vector2, Vector2>> skeleton, out HashSet<Edge> outBorders, out HashSet<Edge> outSpokes, out HashSet<Edge> outSkeleton)
        {
            //Setup result collections
            outBorders = new HashSet<Edge>();
            outSkeleton = new HashSet<Edge>();
            outSpokes = new HashSet<Edge>();

            //setup collection of vertices and a way to lazily construct them
            var vertices = new Dictionary<Vector2, Vertex>();
            Func<Vector2, Vertex> getOrCreate = p =>
            {
                Vertex v;
                if (!vertices.TryGetValue(p, out v))
                {
                    v = new Vertex(p);
                    vertices.Add(p, v);
                }
                return v;
            };

            //Connect around border
            foreach (var border in borders)
                outBorders.Add(Edge.Create(getOrCreate(border.Key), getOrCreate(border.Value), EdgeType.Border));

            //Connect along spokes
            foreach (var spoke in spokes)
                outSpokes.Add(Edge.Create(getOrCreate(spoke.Key), getOrCreate(spoke.Value), EdgeType.Spoke));

            //Connect along skeleton
            foreach (var skele in skeleton)
                outSkeleton.Add(Edge.Create(getOrCreate(skele.Key), getOrCreate(skele.Value), EdgeType.Skeleton));
        }

        public void CloneGraph(out HashSet<Edge> outBorders, out HashSet<Edge> outSpokes, out HashSet<Edge> outSkeleton)
        {
            Initialize(
                _borders.Select(a => new KeyValuePair<Vector2, Vector2>(a.Start.Position, a.End.Position)),
                _spokes.Select(a => new KeyValuePair<Vector2, Vector2>(a.Start.Position, a.End.Position)),
                _skeleton.Select(a => new KeyValuePair<Vector2, Vector2>(a.Start.Position, a.End.Position)),
                out outBorders, out outSpokes, out outSkeleton
            );
        }

        public IEnumerable<IReadOnlyList<Vector2>> Offset(float distance)
        {
            if (distance <= 0)
                throw new ArgumentOutOfRangeException("distance", "distance must be > 0");

            unsafe
            {
                //Generate result
                var result = GenerateOffsetPolygon(_opaqueHandle.ToPointer(), distance);

                try
                {
                    var polygons = (Poly*)result.Start.ToPointer();
                    var results = new Vector2[result.Items][];

                    //Copy data from native memory into array of polygons
                    for (var i = 0; i < result.Items; i++)
                    {
                        var polygon = &polygons[i];

                        //Copy data into array
                        var polyVerts = new Vector2[polygon->VerticesLength];
                        for (var v = 0; v < polygon->VerticesLength; v++)
                            polyVerts[v] = new Vector2(polygon->Vertices[v * 2], polygon->Vertices[v * 2 + 1]);

                        results[i] = polyVerts;
                    }

                    return results;
                }
                finally
                {
                    FreePolyArray(result);
                }
            }
        }

        #region generation
        private static readonly Vector2[][] _noHoles = new Vector2[0][];

        /// <summary>
        /// Generate a straight skeleton for the given input
        /// </summary>
        /// <param name="outer">clockwise wound points indicating the outside of the shape</param>
        /// <param name="holes">clockwise wound points indicating the holes in the shape (or null, if there are no holes)</param>
        /// <returns></returns>
        public static StraightSkeleton Generate(IReadOnlyList<Vector2> outer, IReadOnlyList<IReadOnlyList<Vector2>> holes = null)
        {
            //sanity checks
            holes = SanityCheck(outer, holes);

            //Copy all the data into one big array (so we only need to fix one pointer, and just pass many offsets into it)
            var points = CopyData(outer, holes);

            //Variable to hold results
            Poly result;
            try
            {
                unsafe
                {
                    IntPtr handle;

                    //Fix a pointer to the start of the points array
                    fixed (Point2* pointsPtr = &points[0])
                    {
                        //Outer polygon
                        var outerPoly = new Poly((float*)pointsPtr, outer.Count);

                        //Holes
                        var holePolys = new Poly[holes.Count];
                        var holeStartIndex = outer.Count;
                        for (var i = 0; i < holes.Count; i++)
                        {
                            holePolys[i] = new Poly((float*)(&pointsPtr[holeStartIndex]), holes[i].Count);
                            holeStartIndex += holes[i].Count;
                        }

                        //Generate skeleton
                        if (holePolys.Length > 0)
                        {
                            fixed (Poly* holesPtr = &holePolys[0])
                                handle = new IntPtr(GenerateStraightSkeleton(&outerPoly, holesPtr, holes.Count, &result));
                        }
                        else
                            handle = new IntPtr(GenerateStraightSkeleton(&outerPoly, null, 0, &result));
                    }

                    return ExtractResult(outer, holes, &result, handle);
                }
            }
            finally
            {
                unsafe
                {
                    //We allocate memory in the Result struct (in C++) to store the result data, call this to free up that memory
                    FreePolygonStructMembers(&result);
                }
            }
        }

        private static unsafe StraightSkeleton ExtractResult(IReadOnlyList<Vector2> outer, IReadOnlyList<IReadOnlyList<Vector2>> holes, Poly* result, IntPtr handle)
        {
            //Set of all vertices supplied as input
            var inputVertices = new HashSet<Vector2>(outer);
            inputVertices.UnionWith(holes.SelectMany(h => h));

            //An edge is either border (between 2 input points), spoke (input -> skeleton) or skeleton (skeleton -> skeleton)
            var borders = new HashSet<KeyValuePair<Vector2, Vector2>>();
            var spokes = new HashSet<KeyValuePair<Vector2, Vector2>>();
            var skeleton = new HashSet<KeyValuePair<Vector2, Vector2>>();

            //Extract skeleton edges
            for (var i = 0; i < result->VerticesLength / 4; i++)
            {
                //Two points, which should we use as the start point?
                var a = new Vector2(result->Vertices[i * 4 + 0], result->Vertices[i * 4 + 1]);
                var b = new Vector2(result->Vertices[i * 4 + 2], result->Vertices[i * 4 + 3]);

                var ab = inputVertices.Contains(a);
                var bb = inputVertices.Contains(b);

                if (ab && bb)
                {
                    //Outer->Outer, order by hashcode (so edges are not added twice)
                    borders.Add(OrderByHash(a, b));
                }
                else if (ab & !bb)
                {
                    //Spoke, add outer vertex first (so edges are not added twice)
                    spokes.Add(new KeyValuePair<Vector2, Vector2>(a, b));
                }
                else if (bb)
                {
                    //Spoke, add outer vertex first (so edges are not added twice)
                    spokes.Add(new KeyValuePair<Vector2, Vector2>(b, a));
                }
                else
                {
                    //Skeleton edge, order by hashcode (so edges are not added twice)
                    skeleton.Add(OrderByHash(a, b));
                }
            }

            return new StraightSkeleton(inputVertices, borders, spokes, skeleton, handle);
        }

        private static KeyValuePair<Vector2, Vector2> OrderByHash(Vector2 a, Vector2 b)
        {
            var ah = a.GetHashCode();
            var bh = b.GetHashCode();

            //Check order
            if (ah < bh)
                return new KeyValuePair<Vector2, Vector2>(a, b);
            else
                return new KeyValuePair<Vector2, Vector2>(b, a);
        }

        private static Point2[] CopyData(IReadOnlyList<Vector2> outer, IReadOnlyList<IReadOnlyList<Vector2>> holes)
        {
            var points = new Point2[outer.Count + holes.Sum(h => h.Count)];
            var index = 0;

            //Copy points backwards (because CGAL wants them counter-clowise, but we supply them clockwise)
            for (var i = outer.Count - 1; i >= 0; i--)
                points[index++] = new Point2(outer[i].X, outer[i].Y);

            //Points are copied forward here, because holes are supplied to us clockwise wound and CGAL wants them clockwise
            foreach (var hole in holes)
            {
                for (var j = 0; j < hole.Count; j++)
                    points[index++] = new Point2(hole[j].X, hole[j].Y);
            }
            return points;
        }

        private static IReadOnlyList<IReadOnlyList<Vector2>>  SanityCheck(IReadOnlyList<Vector2> outer, IReadOnlyList<IReadOnlyList<Vector2>> holes)
        {
            if (outer == null)
                throw new ArgumentNullException("outer");
            holes = holes ?? _noHoles;
            return holes;
        }

        [DllImport("CGAL_StraightSkeleton_Wrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe void* GenerateStraightSkeleton(Poly* outer, Poly* holes, int holesCount, Poly* result);

        [DllImport("CGAL_StraightSkeleton_Wrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe PolyArray GenerateOffsetPolygon(void* outer, float distance);

        [DllImport("CGAL_StraightSkeleton_Wrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe void FreePolygonStructMembers(Poly* result);

        [DllImport("CGAL_StraightSkeleton_Wrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe void FreeResultHandle(void* result);

        [DllImport("CGAL_StraightSkeleton_Wrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe void FreePolyArray(PolyArray handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct PolyArray
        {
            public readonly int Items;
            public readonly IntPtr Start;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Point2
        {
            // ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
            private readonly float X;
            private readonly float Y;
            // ReSharper restore PrivateFieldCanBeConvertedToLocalVariable

            public Point2(float x, float y)
            {
                X = x;
                Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct Poly
        {
            public readonly float* Vertices;
            public readonly int VerticesLength;

            public Poly(float* vertices, int verticesLength)
            {
                Vertices = vertices;
                VerticesLength = verticesLength;
            }
        }
        #endregion

        #region disposal
        ~StraightSkeleton()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                //Dispose managed resources
            }

            //Dispose unmanaged
            unsafe
            {
                FreeResultHandle(_opaqueHandle.ToPointer());
            }
        }
        #endregion
    }
}
