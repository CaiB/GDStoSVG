using System;
using System.Collections.Generic;

namespace GDStoSVG
{
    public class GDSData
    {
        /// <summary> The name of the library contained in the GDS data. </summary>
        public static string LibraryName { get; set; } = "Invalid Library Name";

        /// <summary> All of the structures contained in the GDS file, accessable by their name (<see cref="Structure.Name"/>). </summary>
        public static Dictionary<string, Structure> Structures { get; private set; } = new Dictionary<string, Structure>();

        /// <summary> The last structure read in form GDS data. This is usually the top-level structure, and the root of the default export. </summary>
        public static Structure? LastStructure;

        /// <summary> Looks through all data that was read in to find all layers in use, then outputs this info to the console. </summary>
        public static void ScanLayers()
        {
            List<short> Layers = new List<short>();
            foreach(Structure Struct in Structures.Values)
            {
                if(Struct.Elements == null) { continue; }
                foreach(Element El in Struct.Elements)
                {
                    short? NewLayer = null;
                    if (El is Boundary Boundary) { NewLayer = Boundary.Layer; }
                    else if (El is Path Path) { NewLayer = Path.Layer; }
                    else if (El is Text Text) { NewLayer = Text.Layer; }
                    else if (El is Node Node) { NewLayer = Node.Layer; }
                    else if (El is Box Box) { NewLayer = Box.Layer; }

                    if (NewLayer != null && !Layers.Contains((short)NewLayer)) { Layers.Add((short)NewLayer); }
                }
            }
            Layers.Sort();
            Console.WriteLine("Found " + Layers.Count + " layers:");
            foreach (short Layer in Layers)
            {
                string LayerName = "[NOT ASSIGNED]";
                if (LayerConfig.Layers.ContainsKey(Layer)) { LayerName = LayerConfig.Layers[Layer].Name; }
                Console.WriteLine("  Layer " + Layer + " -> " + LayerName);
            }
        }
    }
}
