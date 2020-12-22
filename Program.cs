using System;

namespace GDStoSVG
{
    class Program
    {
        static void Main(string[] args)
        {
            string CSVFile = "LayerConfig.csv";
            LayerConfig.ReadConfig(CSVFile);
            string GDSFile = "RF_MUX_v1.gds";
            GDSReader GDS = new GDSReader();
            GDS.ReadFile(GDSFile);
            GDSData.ScanLayers();
            string SVGFile = "RF_MUX_v1.svg";
            SVGWriter SVG = new SVGWriter(SVGFile);
            SVG.WriteRoot(GDSData.Structures["REG4"]);
            SVG.Finish();

            // Expected parameters
            // CSV file, GDS file, SVG file, layer detect mode switch
        }
    }
}
