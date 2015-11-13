using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace CGAL_StraightSkeleton_Dotnet
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Point2
    {
        public readonly float X;
        public readonly float Y;

        public Point2(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct Poly
    {
        public readonly float* Vertices;
        public readonly int VerticesLength;

        public Poly(float* vertices, int verticesLength)
        {
            Vertices = vertices;
            VerticesLength = verticesLength;
        }
    }

    public class StraightSkeleton
    {
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
            if (outer == null)
                throw new ArgumentNullException("outer");
            holes = holes ?? _noHoles;

            //Copy all the data into one big array (so we only need to fix one pointer, and just pass many offsets into it)
            var points = new Point2[outer.Length + holes.Sum(h => h.Length)];
            var index = 0;

            //Copy points backwards (because CGAL wants them counter-clowise, but we supply them clockwise)
            for (var i = outer.Length - 1; i >= 0; i--)
                points[index++] = new Point2(outer[i].X, outer[i].Y);

            //Points are copied forward here, because holes are supplied to us clockwise wound and CGAL wants them clockwise
            foreach (var hole in holes)
                for (var j = 0; j < hole.Length; j++)
                    points[index++] = new Point2(hole[j].X, hole[j].Y);

            Poly result;
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
                        fixed (Poly* holesPtr = &holePolys[0])
                            GenerateStraightSkeleton(&outerPoly, holesPtr, holes.Length, &result);
                    }
                }

                //Extract skeleton vertices
                var vertices = new Vector2[result.VerticesLength / 2];
                for (var i = 0; i < vertices.Length; i++)
                {
                    unsafe
                    {
                        vertices[i] = new Vector2(result.Vertices[i * 2], result.Vertices[i * 2 + 1]);
                    }
                }

                ////Extract data from result
                //var b = new StringBuilder();
                //b.Append("<svg width=\"1000\" height=\"1000\"><g transform=\"translate(210, 210)\"><path stroke=\"black\" d=\"");
                //for (var i = 0; i < vertices.Length / 2; i++)
                //    b.Append(string.Format("M {0} {1} L{2} {3} ", vertices[i * 2].X * 10, vertices[i * 2].Y * 10, vertices[i * 2 + 1].X * 10, vertices[i * 2 + 1].Y * 10));
                //b.Append("\"></path></g></svg>");
                //Console.WriteLine(b);
            }
            finally
            {
                unsafe
                {
                    //We allocate memory in the Result struct (in C++) to store the result data, call this to free up that memory
                    FreePolygonStructMembers(&result);
                }
            }

            return null;
        }

        [DllImport("CGAL_StraightSkeleton_Wrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe void GenerateStraightSkeleton(Poly* outer, Poly* holes, int holesCount, Poly* result);

        [DllImport("CGAL_StraightSkeleton_Wrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe void FreePolygonStructMembers(Poly* result);
    }
}
