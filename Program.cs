using System;
using System.IO;

namespace GDStoSVG
{
    public class Program
    {

        // TODO LIST
        // Implement rendering text and other items
        // Text render only certain levels deep

        public static bool Debug = false;
        public static bool Info = false;

        static void Main(string[] args)
        {
            if(args.Length < 1) { PrintHelp(); return; }

            string? GDSFile = null;
            string? CSVFile = null;
            string? SVGFile = null;
            string? TopUnit = null;

            for(int i = 0; i < args.Length; i++)
            {
                if (i == 0 || (args[i].EndsWith(".gds", StringComparison.OrdinalIgnoreCase) && GDSFile == null))
                {
                    GDSFile = args[i];
                }
                else if (args[i].EndsWith(".csv", StringComparison.OrdinalIgnoreCase) && CSVFile == null)
                {
                    CSVFile = args[i];
                }
                else if (args[i].EndsWith(".svg", StringComparison.OrdinalIgnoreCase) && SVGFile == null)
                {
                    SVGFile = args[i];
                }
                else if (args[i].Equals("-unit", StringComparison.OrdinalIgnoreCase) && args.Length > i + 1)
                {
                    TopUnit = args[i + 1];
                    i++;
                }
                else if (args[i].Equals("-info", StringComparison.OrdinalIgnoreCase))
                {
                    Info = true;
                }
                else if (args[i].Equals("-debug", StringComparison.OrdinalIgnoreCase))
                {
                    Debug = true;
                }
            }

            if (GDSFile == null) { PrintHelp(); return; }
            if (CSVFile == null) { Info = true; } // Turn on info output if no CSV was specified (probably not set up yet).
            if (SVGFile == null) { SVGFile = System.IO.Path.GetFileNameWithoutExtension(GDSFile) + ".svg"; } // Set SVG name to be same as GDS if none is specified.
            if (Debug) { Info = true; }

            if (CSVFile != null && File.Exists(CSVFile)) { LayerConfig.ReadConfig(CSVFile); }
            else { Console.WriteLine("CSV file not specified or not found."); }

            if (!File.Exists(GDSFile)) { Console.WriteLine("Could not find GDS file \"{0}\".", GDSFile); return; }

            GDSReader GDS = new GDSReader();
            if (Debug) { GDS.TestDoubleParse(); }
            GDS.ReadFile(GDSFile);
            if (Info) { GDSData.ScanLayers(); }

            if (TopUnit == null && GDSData.LastStructure != null) { TopUnit = GDSData.LastStructure.Name; }
            if (TopUnit == null) { Console.WriteLine("Could not determine top-level unit."); return; }
            Console.WriteLine("Outputting unit \"{0}\" to \"{1}\".", TopUnit, SVGFile);

            SVGWriter SVG = new SVGWriter(SVGFile);
            SVG.WriteRoot(GDSData.Structures[TopUnit]);
            SVG.Finish();
            Console.WriteLine("Done!");
        }

        /// <summary> Outputs basic usage information to the console. </summary>
        private static void PrintHelp()
        {
            Console.WriteLine("GDStoSVG: Converts GDSII data into SVG graphics for printing or viewing.");
            Console.WriteLine("Usage Notes: (<> means required, [] means optional)");
            Console.WriteLine("GDStoSVG.exe <GDS file> [CSV file] [SVG file] [-unit NAME] [-info] [-debug]");
            Console.WriteLine("  <GDS file>: Name of the GDSII file to read");
            Console.WriteLine("  [CSV file]: Name of the CSV file containing layer definitions");
            Console.WriteLine("    CSV expected format: No header, each line should contain:");
            Console.WriteLine("    <Name>, <ID in range -32768 to 32767>, <Colour, RRGGBB hex>, <Opacity in range 0.0 to 1.0>");
            Console.WriteLine("    Layers will be stacked in list order, with the bottom line of CSV file on top in SVG.");
            Console.WriteLine("  [-unit NAME]: Name of the top-level design unit to output, including all child elements.");
            Console.WriteLine("  [-info]: Outputs extra info about layers and units to help you in setting up output.");
            Console.WriteLine("  [-debug]: Use this if the program is misbehaving and you need to ask the developer.");
        }
    }
}
