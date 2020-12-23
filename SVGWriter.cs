using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GDStoSVG
{
    public class SVGWriter
    {
        private readonly StreamWriter Writer;
        private readonly Layer UnknownLayer;

        public SVGWriter(string fileName)
        {
            this.Writer = new StreamWriter(fileName);
            this.Writer.AutoFlush = false; // Don't flush after every write.
            this.Writer.WriteLine(@"<?xml version=""1.0"" standalone=""no""?>");
            this.Writer.WriteLine(@"<!DOCTYPE svg PUBLIC "" -//W3C//DTD SVG 1.1//EN"" ""http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd"">");
            this.Writer.WriteLine(@"<svg viewBox = ""0 0 200 200"" version = ""1.1"">"); // TODO Update viewbox
            this.UnknownLayer = new Layer
            {
                Name = "Unassigned Layer",
                Colour = 0x7F7F7F,
                ID = -1,
                Opacity = 0.5F
            };
        }

        /// <summary> Provided a root Structure, writes all contained elements and child structures/elements. </summary>
        /// <param name="structure"> The root structure to output all contents of. </param>
        public void WriteRoot(Structure structure)
        {
            if (structure.Elements == null) { return; } // No child elements, output nothing.
            foreach(Element Element in structure.Elements)
            {
                WriteElement(Element);
            }
        }

        public void WriteRoot(Structure structure, Transform trans)
        {
            if (structure.Elements == null) { return; } // No child elements, output nothing.
            foreach (Element Element in structure.Elements)
            {
                WriteElement(Element, trans);
            }
        }

        /// <summary> Writes an element, as well as all referenced elements if it is a reference-type element. </summary>
        /// <param name="element"> The element to write to SVG. </param>
        /// <param name="trans"> The transform to apply to the element, null if default should be used. </param>
        public void WriteElement(Element element, Transform? trans = null)
        {
            if (trans == null) { trans = Transform.Default; }
            switch(element)
            {
                case Boundary Boundary: WriteBoundary(Boundary, trans); break;
                case Path Path: WritePath(Path, trans); break;
                case StructureRef StructRef: WriteStructRef(StructRef, trans); break;
                case ArrayRef ArrayRef: WriteArrayRef(ArrayRef, trans); break;
                case Text Text: WriteText(Text, trans); break;
                case Node Node: WriteNode(Node, trans); break;
                case Box Box: WriteBox(Box, trans); break;
                default: Console.WriteLine("Unknown element type, not writing to SVG: " + element); break;
            }
        }

        public void WriteBoundary(Boundary bound, Transform trans)
        {
            if (!bound.Check()) { Console.WriteLine("Skipping invalid boundary"); return; } // Layer, Coords are non-null after
            if (bound.Coords!.Length < 3) { Console.WriteLine("Skipping boundary with less than 3 points."); return; }
            this.Writer.Write(@"<polygon points=""");
            for(int i = 0; i < bound.Coords.Length - 1; i++) // Last element = first, so we don't write the last one.
            {
                double X = bound.Coords[i].Item1 + trans.PositionOffset.Item1;
                double Y = trans.PositionOffset.Item2 + (trans.YReflect ? -bound.Coords[i].Item2 : bound.Coords[i].Item2);
                X = (X * Math.Cos(trans.Angle / 180 * Math.PI)) - (Y * Math.Sin(trans.Angle / 180 * Math.PI));
                Y = (Y * Math.Cos(trans.Angle / 180 * Math.PI)) + (X * Math.Sin(trans.Angle / 180 * Math.PI));
                this.Writer.Write("{0},{1}", X, -Y); // SVG has inverted Y
                if (i != bound.Coords.Length - 2) { this.Writer.Write(' '); }
            }
            Layer Layer = GetLayer((short)bound.Layer!);
            this.Writer.WriteLine(@""" fill=""#" + Layer.Colour.ToString("X6") + @""" opacity=""" + Layer.Opacity + @""" />");
        }

        public void WritePath(Path path, Transform trans)
        {
            if (!path.Check()) { Console.WriteLine("Skipping invalid path"); return; } // Layer, Coords are non-null after
            if (path.Coords!.Length < 2) { Console.WriteLine("Skipping path with less than 2 points."); return; }
            this.Writer.Write(@"<polyline points=""");
            for(int i = 0; i < path.Coords.Length; i++)
            {
                double X = path.Coords[i].Item1 + trans.PositionOffset.Item1;
                double Y = trans.PositionOffset.Item2 + (trans.YReflect ? -path.Coords[i].Item2 : path.Coords[i].Item2);
                X = (X * Math.Cos(trans.Angle / 180 * Math.PI)) - (Y * Math.Sin(trans.Angle / 180 * Math.PI));
                Y = (Y * Math.Cos(trans.Angle / 180 * Math.PI)) + (X * Math.Sin(trans.Angle / 180 * Math.PI));
                this.Writer.Write("{0},{1}", X, -Y); // SVG has inverted Y
                if (i != path.Coords.Length - 1) { this.Writer.Write(' '); }
            }

            string EndcapType = "butt"; // Doesn't support type 4.
            if (path.PathType == 1) { EndcapType = "round"; }
            if (path.PathType == 2) { EndcapType = "square"; }
            Layer Layer = GetLayer((short)path.Layer!);
            string Colour = Layer.Colour.ToString("X6");
            double Width = path.Width < 0 ? -path.Width : path.Width * trans.Magnification;

            this.Writer.WriteLine(@""" stroke=""#" + Colour + @""" stroke-width=""" + Width + @""" opacity=""" + Layer.Opacity + @""" stroke-linecap=""" + EndcapType + @""" />");
        }

        public void WriteStructRef(StructureRef structRef, Transform trans)
        {
            if (!structRef.Check()) { Console.WriteLine("Skipping invalid structure reference"); return; } // StructureName, Coords are non-null after
            Structure Struct = GDSData.Structures[structRef.StructureName!];
            structRef.Transform.PositionOffset = structRef.Coords![0];
            Transform NewTrans = structRef.Transform.ApplyParent(trans);
            WriteRoot(Struct, NewTrans);
        }

        public void WriteArrayRef(ArrayRef arrayRef, Transform trans)
        {

        }

        public void WriteText(Text text, Transform trans)
        {

        }

        public void WriteNode(Node node, Transform trans)
        {

        }

        public void WriteBox(Box box, Transform trans)
        {
            if (!box.Check()) { Console.WriteLine("Skipping invalid box"); return; } // Layer, Coords are non-null after
            if (box.Coords!.Length != 5) { Console.WriteLine("Skipping box with point count that is not 5."); return; }
            this.Writer.Write(@"<polygon points=""");
            for (int i = 0; i < box.Coords.Length - 1; i++) // Last element = first, so we don't write the last one.
            {
                this.Writer.Write("{0},{1}", box.Coords[i].Item1, -box.Coords[i].Item2);
                if (i != box.Coords.Length - 1) { this.Writer.Write(' '); }
            }
            Layer Layer = GetLayer((short)box.Layer!);
            this.Writer.Write(@""" fill=""#" + Layer.Colour.ToString("X6") + @""" opacity=""" + Layer.Opacity + @""" />");
        }

        public void Finish()
        {
            this.Writer.WriteLine("</svg>");
            this.Writer.Flush();
            this.Writer.Close();
            this.Writer.Dispose();
        }

        private Layer GetLayer(short layerID)
        {
            if (LayerConfig.Layers.ContainsKey(layerID)) { return LayerConfig.Layers[layerID]; }
            else
            {
                this.UnknownLayer.ID = layerID;
                return this.UnknownLayer;
            }
        }

    }
}
