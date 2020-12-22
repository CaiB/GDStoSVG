using System;
using System.Collections.Generic;

namespace GDStoSVG
{
    public class GDSData
    {
        public static string LibraryName { get; set; } = "Invalid Library Name";
        public static Dictionary<string, Structure> Structures { get; private set; } = new Dictionary<string, Structure>();
        public static Structure? LastStructure;

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
                Console.WriteLine("> " + Layer + " -> " + LayerName);
            }
        }
    }
}
