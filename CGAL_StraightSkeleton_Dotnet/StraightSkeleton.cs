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
        private readonly HashSet<Vector2> _inputVertices;
        private readonly IntPtr _opaqueHandle;

        private readonly HashSet<KeyValuePair<Vector2, Vector2>> _borders;
        /// <summary>
        /// Edges around the outside of the shape
        /// </summary>
        public IEnumerable<KeyValuePair<Vector2, Vector2>> Borders
        {
            get { return _spokes; }
        }

        private readonly HashSet<KeyValuePair<Vector2, Vector2>> _spokes;
        /// <summary>
        /// Edges connecting outside of shape to the skeleton
        /// </summary>
        public IEnumerable<KeyValuePair<Vector2, Vector2>> Spokes
        {
            get { return _spokes; }
        }

        private readonly HashSet<KeyValuePair<Vector2, Vector2>> _skeleton;
        /// <summary>
        /// The spine of the skeleton
        /// </summary>
        public IEnumerable<KeyValuePair<Vector2, Vector2>> Skeleton
        {
            get { return _skeleton; }
        }

        private StraightSkeleton(HashSet<Vector2> inputVertices, HashSet<KeyValuePair<Vector2, Vector2>> borders, HashSet<KeyValuePair<Vector2, Vector2>> spokes, HashSet<KeyValuePair<Vector2, Vector2>> skeleton, IntPtr opaqueHandle)
        {
            _inputVertices = inputVertices;
            _borders = borders;
            _spokes = spokes;
            _skeleton = skeleton;
            _opaqueHandle = opaqueHandle;
        }

        public void Offset(float distance)
        {
            unsafe
            {
                //Generate result
                var result = GenerateOffsetPolygon(_opaqueHandle.ToPointer(), distance);

                try
                {
                    //Extract result items from array
                    var polygons = (Poly*)result.Start.ToPointer();

                    for (int i = 0; i < result.Items; i++)
                    {
                        var polygon = &polygons[i];
                    }
                }
                finally
                {
                    FreePolyArray(result);
                }
            }
        }

        //public string ToSvg()
        //{
        //    //Extract data from result
        //    var svg = new StringBuilder();
        //    svg.Append("<svg width=\"1000\" height=\"1000\"><g transform=\"translate(210, 210)\"><path stroke=\"black\" d=\"");
        //    foreach (var edge in spokes)
        //        svg.Append(string.Format("M {0} {1} L{2} {3} ", edge.Key.X * 10, edge.Key.Y * 10, edge.Value.X * 10, edge.Value.Y * 10));
        //    foreach (var edge in skeleton)
        //        svg.Append(string.Format("M {0} {1} L{2} {3} ", edge.Key.X * 10, edge.Key.Y * 10, edge.Value.X * 10, edge.Value.Y * 10));
        //    foreach (var edge in borders)
        //        svg.Append(string.Format("M {0} {1} L{2} {3} ", edge.Key.X * 10, edge.Key.Y * 10, edge.Value.X * 10, edge.Value.Y * 10));
        //    svg.Append("\"></path></g></svg>");
        //    Console.WriteLine(svg);
        //}

        #region generation
        private static readonly Vector2[][] _noHoles = new Vector2[0][];

        /// <summary>
        /// Generate a straight skeleton for the given input
        /// </summary>
        /// <param name="outer">clockwise wound points indicating the outside of the shape</param>
        /// <param name="holes">clockwise wound points indicating the holes in the shape (or null, if there are no holes)</param>
        /// <returns></returns>
        public static StraightSkeleton Generate(Vector2[] outer, Vector2[][] holes = null)
        {
            //sanity checks
            holes = SanityCheck(outer, holes);

            //Copy all the data into one big array (so we only need to fix one pointer, and just pass many offsets into it)
            var points = CopyData(outer, holes);

            //Variable to hold results
            Poly result;
            IntPtr handle = IntPtr.Zero;
            try
            {
                unsafe
                {
                    //Fix a pointer to the start of the points array
                    fixed (Point2* pointsPtr = &points[0])
                    {
                        //Outer polygon
                        var outerPoly = new Poly((float*)pointsPtr, outer.Length);

                        //Holes
                        var holePolys = new Poly[holes.Length];
                        var holeStartIndex = outer.Length;
                        for (var i = 0; i < holes.Length; i++)
                        {
                            holePolys[i] = new Poly((float*)(&pointsPtr[holeStartIndex]), holes[i].Length);
                            holeStartIndex += holes[i].Length;
                        }

                        //Generate skeleton
                        if (holePolys.Length > 0)
                        {
                            fixed (Poly* holesPtr = &holePolys[0])
                                handle = new IntPtr(GenerateStraightSkeleton(&outerPoly, holesPtr, holes.Length, &result));
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

        private static unsafe StraightSkeleton ExtractResult(Vector2[] outer, Vector2[][] holes, Poly* result, IntPtr handle)
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

        private static Point2[] CopyData(Vector2[] outer, Vector2[][] holes)
        {
            var points = new Point2[outer.Length + holes.Sum(h => h.Length)];
            var index = 0;

            //Copy points backwards (because CGAL wants them counter-clowise, but we supply them clockwise)
            for (var i = outer.Length - 1; i >= 0; i--)
                points[index++] = new Point2(outer[i].X, outer[i].Y);

            //Points are copied forward here, because holes are supplied to us clockwise wound and CGAL wants them clockwise
            foreach (var hole in holes)
            {
                for (var j = 0; j < hole.Length; j++)
                    points[index++] = new Point2(hole[j].X, hole[j].Y);
            }
            return points;
        }

        private static Vector2[][] SanityCheck(Vector2[] outer, Vector2[][] holes)
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
