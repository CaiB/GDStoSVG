using System;

namespace GDStoSVG
{
    class Program
    {
        static void Main(string[] args)
        {
            //Console.WriteLine("Input GDS File: ");
            string GDSFile = "RF_MUX_v1.gds";// Console.ReadLine();
            GDSReader GDS = new GDSReader();
            GDS.ReadFile(GDSFile);
            GDSData.ScanLayers();
            string SVGFile = "RF_MUX_v1.svg";
            SVGWriter SVG = new SVGWriter(SVGFile);

            SVG.Finish();

            // Expected parameters
            // CSV file, GDS file, SVG file, layer detect mode switch
        }
    }
}
