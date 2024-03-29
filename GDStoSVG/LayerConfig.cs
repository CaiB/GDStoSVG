﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace GDStoSVG;

public class LayerConfig
{
    /// <summary> The currently configured layers </summary>
    public static Dictionary<short, Layer> Layers { get; private set; } = new Dictionary<short, Layer>();

    /// <summary> Populates <see cref="Layers"/> with data from a CSV file. </summary>
    /// <remarks> CSV file should be formatted with no header, with 4 entries per line: [Name:string], [ID:short], [Colour:uint hex], [Opacity:float] </remarks>
    /// <param name="configFile"> The filename of a CSV file to read from. </param>
    public static void ReadConfig(string configFile)
    {
        List<Layer> LayerList = new();
        using (StreamReader Reader = new(configFile))
        {
            string? Line = Reader.ReadLine();
            Regex CSVParse = new(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");
            int LineNum = 1;
            while (Line != null)
            {
                string[] Parts = CSVParse.Split(Line);
                if (Parts.Length < 4)
                {
                    Console.WriteLine("Missing data in CSV file on line " + LineNum);
                    Line = Reader.ReadLine();
                    LineNum++;
                    continue;
                }

                Layer Layer = new()
                {
                    Name = Parts[0],
                    ID = short.Parse(Parts[1]),
                    Colour = Convert.ToUInt32(Parts[2], 16), // hex
                    Opacity = float.Parse(Parts[3]),
                    SortOrder = LineNum - 1 // starts at 1, so offset down 1
                };
                LayerList.Add(Layer);
                Line = Reader.ReadLine();
                LineNum++;
            }
        }
        foreach (Layer Layer in LayerList) { Layers.Add(Layer.ID, Layer); }
    }
}

/// <summary> Stores info about a layer in the GDS file, corresponding to a certain material/process in the chip. E.g. polysilicon for gates. </summary>
public class Layer
{
    /// <summary> Name used to label output polygons. Any name valid in SVG is fine, does not matter to GDSII import. </summary>
    public string Name { get; set; } = "Unnamed Layer";

    /// <summary> The layer ID used in the GDSII file. </summary>
    public short ID { get; set; }

    /// <summary> The colour to give the layer in the output SVG. </summary>
    public uint Colour { get; set; }

    /// <summary> The opacity to assign the objects in the SVG file. </summary>
    public float Opacity { get; set; }

    /// <summary> Determines the order in which the layers appear in the SVG output. </summary>
    public int SortOrder { get; set; }
}
