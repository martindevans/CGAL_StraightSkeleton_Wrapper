using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
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

            Console.WriteLine();
            Console.Title = string.Format("Elapsed: {0}ms", w.ElapsedMilliseconds);
            Console.ReadLine();

            ssk.Offset(3);
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
