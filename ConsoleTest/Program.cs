using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using CGAL_StraightSkeleton_Dotnet;

namespace ConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Stopwatch w = new Stopwatch();
            w.Start();

            var ssk = StraightSkeleton.Generate(new Vector2[] {
                new Vector2(-10, 10),
                new Vector2(10, 10),
                new Vector2(10, -2),
                new Vector2(5, 0),
                new Vector2(7, -10),
                new Vector2(-7, -10),
                new Vector2(-5, 0),
                new Vector2(-10, -2),
            }, new Vector2[][] {
                new Vector2[] {
                    new Vector2(2, 2),
                    new Vector2(2, -2),
                    new Vector2(-2, -2),
                    new Vector2(-2, 2)
                }
            });

            w.Stop();

            Stopwatch w2 = new Stopwatch();
            w2.Start();

            //Extract data from result
            var svg = new StringBuilder();
            svg.Append("<svg width=\"1000\" height=\"1000\"><g transform=\"translate(210, 210)\">");

            for (var i = 1; i < 10; i++)
            {
                var offset = ssk.Offset(i / 2f);

                foreach (var polygon in offset)
                {
                    svg.Append("<path fill=\"none\" stroke=\"green\" d=\"");
                    svg.Append(string.Format("M {0} {1} ", polygon[0].X * 10, polygon[0].Y * 10));
                    svg.Append(string.Join(" ", polygon.Select(v => string.Format(" L {0} {1} ", v.X * 10, v.Y * 10))));
                    svg.Append("z\"></path>");
                }
            }

            svg.Append("<path fill=\"none\" stroke=\"hotpink\" d=\"");
            //foreach (var edge in ssk.Spokes)
            //    svg.Append(string.Format("M {0} {1} L{2} {3} ", edge.Key.X * 10, edge.Key.Y * 10, edge.Value.X * 10, edge.Value.Y * 10));
            foreach (var edge in ssk.Skeleton)
                svg.Append(string.Format("M {0} {1} L{2} {3} ", edge.Key.X * 10, edge.Key.Y * 10, edge.Value.X * 10, edge.Value.Y * 10));
            //foreach (var edge in ssk.Borders)
            //    svg.Append(string.Format("M {0} {1} L{2} {3} ", edge.Key.X * 10, edge.Key.Y * 10, edge.Value.X * 10, edge.Value.Y * 10));
            svg.Append("\"></path>");

            svg.Append("</g></svg>");
            Console.WriteLine(svg);

            Console.Title = string.Format("Elapsed: {0}ms {1}ms", w.ElapsedMilliseconds, w2.ElapsedMilliseconds);

            Console.ReadLine();
        }

        [DllImport("Kernel32.dll")]
        private static extern IntPtr LoadLibrary(string path);

        private static void Load(string path)
        {
            var lib = LoadLibrary(path);
            Console.WriteLine("{1}\t\t\t{0}", path, LoadLibrary(path));
            if (lib == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
                //Console.WriteLine(" --- ERROR: {0}", Marshal.GetLastWin32Error());
            }
        }
    }
}
