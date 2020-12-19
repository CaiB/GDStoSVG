using System;

namespace GDStoSVG
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Input GDS File: ");
            string GDSFile = Console.ReadLine();
            GDSReader GDS = new GDSReader();
            GDS.ReadFile(GDSFile);

            // Expected parameters
            // CSV file, GDS file, SVG file, layer detect mode switch
        }
    }
}
