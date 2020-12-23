using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GDStoSVG
{
    public class SVGWriter
    {
        /// <summary> The writer used to output to SVG file. </summary>
        private readonly StreamWriter Writer;

        /// <summary> The placeholder layer used when one has not been defined in CSV data. </summary>
        private readonly Layer UnknownLayer;

        /// <summary> Holds all lines that will be stored in the SVG file, organized by <see cref="Layer.ID"/>. </summary>
        private readonly Dictionary<short, List<string>> Output = new Dictionary<short, List<string>>();

        private int MinX = int.MaxValue;
        private int MinY = int.MaxValue;
        private int MaxX = int.MinValue;
        private int MaxY = int.MinValue;

        /// <summary> Prepares the SVG file for writing data. </summary>
        /// <param name="fileName"> The SVG file to write to. If it exists, it will be overwritten. </param>
        public SVGWriter(string fileName)
        {
            this.Writer = new StreamWriter(fileName);
            this.Writer.AutoFlush = false; // Don't flush after every write.
            this.Writer.WriteLine(@"<?xml version=""1.0"" standalone=""no""?>");
            this.Writer.WriteLine(@"<!DOCTYPE svg PUBLIC "" -//W3C//DTD SVG 1.1//EN"" ""http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd"">");
            this.UnknownLayer = new Layer
            {
                Colour = 0x7F7F7F,
                Opacity = 0.5F,
                SortOrder = int.MaxValue
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

            this.Writer.WriteLine("<svg viewBox=\"{0} {1} {2} {3}\" version=\"1.1\">", this.MinX, -this.MaxY, this.MaxX, -this.MinY);

            List<Layer> LayersSorted = LayerConfig.Layers.Values.OrderBy(x => x.SortOrder).ToList(); // TODO Check order and see if help needs to be updated
            foreach(Layer Layer in LayersSorted)
            {
                this.Writer.WriteLine(@"<g id=""" + Layer.Name + @""">");
                foreach (string Line in this.Output[Layer.ID]) { this.Writer.WriteLine(Line); }
                this.Writer.WriteLine("</g>");
            }
        }

        /// <summary> Writes a structure and all child elements, with a <see cref="Transform"/> applied. </summary>
        /// <param name="structure"> The structure to output. </param>
        /// <param name="trans"> The transform to apply to the structure and child elements. </param>
        public void WriteStructure(Structure structure, Transform trans)
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
                //case ArrayRef ArrayRef: WriteArrayRef(ArrayRef, trans); break;
                case Text Text: WriteText(Text, trans); break;
                //case Node Node: WriteNode(Node, trans); break;
                case Box Box: WriteBox(Box, trans); break;
                default: Console.WriteLine("Unknown element type, not writing to SVG: " + element); break;
            }
        }

        public void WriteBoundary(Boundary bound, Transform trans)
        {
            if (!bound.Check()) { Console.WriteLine("Skipping invalid boundary"); return; } // Layer, Coords are non-null after
            if (bound.Coords!.Length < 3) { Console.WriteLine("Skipping boundary with less than 3 points."); return; }
            if (!this.Output.ContainsKey((short)bound.Layer!)) { this.Output.Add((short)bound.Layer, new List<string>()); }

            string Out = @"<polygon points=""";
            for(int i = 0; i < bound.Coords.Length - 1; i++) // Last element = first, so we don't write the last one.
            {
                double X = bound.Coords[i].Item1;
                double Y = trans.YReflect ? -bound.Coords[i].Item2 : bound.Coords[i].Item2;
                X = (X * Math.Cos(trans.Angle / 180 * Math.PI)) - (Y * Math.Sin(trans.Angle / 180 * Math.PI));
                Y = (Y * Math.Cos(trans.Angle / 180 * Math.PI)) + (X * Math.Sin(trans.Angle / 180 * Math.PI));
                X += trans.PositionOffset.Item1;
                Y += trans.PositionOffset.Item2;
                Out += string.Format("{0},{1}", X, -Y); // SVG has inverted Y
                if (i != bound.Coords.Length - 2) { Out += ' '; }
                UpdateExtents(X, Y);
            }
            Layer Layer = GetLayer((short)bound.Layer!);
            Out += string.Format(@""" fill=""#" + Layer.Colour.ToString("X6") + @""" opacity=""" + Layer.Opacity + @""" />");
            this.Output[(short)bound.Layer].Add(Out);
        }

        public void WritePath(Path path, Transform trans)
        {
            if (!path.Check()) { Console.WriteLine("Skipping invalid path"); return; } // Layer, Coords are non-null after
            if (path.Coords!.Length < 2) { Console.WriteLine("Skipping path with less than 2 points."); return; }
            if (!this.Output.ContainsKey((short)path.Layer!)) { this.Output.Add((short)path.Layer, new List<string>()); }

            string Out = @"<polyline points=""";
            for(int i = 0; i < path.Coords.Length; i++)
            {
                double X = path.Coords[i].Item1;
                double Y = trans.YReflect ? -path.Coords[i].Item2 : path.Coords[i].Item2;
                X = (X * Math.Cos(trans.Angle / 180 * Math.PI)) - (Y * Math.Sin(trans.Angle / 180 * Math.PI));
                Y = (Y * Math.Cos(trans.Angle / 180 * Math.PI)) + (X * Math.Sin(trans.Angle / 180 * Math.PI));
                X += trans.PositionOffset.Item1;
                Y += trans.PositionOffset.Item2;
                Out += string.Format("{0},{1}", X, -Y); // SVG has inverted Y
                if (i != path.Coords.Length - 1) { Out += ' '; }
                UpdateExtents(X, Y);
            }

            string EndcapType = "butt"; // Doesn't support type 4.
            if (path.PathType == 1) { EndcapType = "round"; }
            if (path.PathType == 2) { EndcapType = "square"; }
            Layer Layer = GetLayer((short)path.Layer!);
            string Colour = Layer.Colour.ToString("X6");
            double Width = path.Width < 0 ? -path.Width : path.Width * trans.Magnification;

            Out += string.Format(@""" stroke=""#" + Colour + @""" stroke-width=""" + Width + @""" opacity=""" + Layer.Opacity + @""" stroke-linecap=""" + EndcapType + @""" />");
            this.Output[(short)path.Layer].Add(Out);
        }

        public void WriteStructRef(StructureRef structRef, Transform trans)
        {
            if (!structRef.Check()) { Console.WriteLine("Skipping invalid structure reference"); return; } // StructureName, Coords are non-null after
            Structure Struct = GDSData.Structures[structRef.StructureName!];
            structRef.Transform.PositionOffset = structRef.Coords![0];
            Transform NewTrans = structRef.Transform.ApplyParent(trans);
            WriteStructure(Struct, NewTrans);
        }

        public void WriteArrayRef(ArrayRef arrayRef, Transform trans)
        {

        }

        public void WriteText(Text text, Transform trans) // TODO add text support
        {
            // if (!this.Output.ContainsKey((short)text.Layer!)) { this.Output.Add((short)text.Layer, new List<string>()); }
        }

        public void WriteNode(Node node, Transform trans)
        {
            // if (!this.Output.ContainsKey((short)node.Layer!)) { this.Output.Add((short)node.Layer, new List<string>()); }
        }

        public void WriteBox(Box box, Transform trans)
        {
            if (!box.Check()) { Console.WriteLine("Skipping invalid box"); return; } // Layer, Coords are non-null after
            if (box.Coords!.Length != 5) { Console.WriteLine("Skipping box with point count that is not 5."); return; }
            if (!this.Output.ContainsKey((short)box.Layer!)) { this.Output.Add((short)box.Layer, new List<string>()); }

            string Out = @"<polygon points=""";
            for (int i = 0; i < box.Coords.Length - 1; i++) // Last element = first, so we don't write the last one.
            {
                double X = box.Coords[i].Item1;
                double Y = trans.YReflect ? -box.Coords[i].Item2 : box.Coords[i].Item2;
                X = (X * Math.Cos(trans.Angle / 180 * Math.PI)) - (Y * Math.Sin(trans.Angle / 180 * Math.PI));
                Y = (Y * Math.Cos(trans.Angle / 180 * Math.PI)) + (X * Math.Sin(trans.Angle / 180 * Math.PI));
                X += trans.PositionOffset.Item1;
                Y += trans.PositionOffset.Item2;
                Out += string.Format("{0},{1}", X, -Y); // SVG has inverted Y
                if (i != box.Coords.Length - 2) { Out += ' '; }
                UpdateExtents(X, Y);
            }
            Layer Layer = GetLayer((short)box.Layer);
            Out += string.Format(@""" fill=""#" + Layer.Colour.ToString("X6") + @""" opacity=""" + Layer.Opacity + @""" />");
            this.Output[(short)box.Layer].Add(Out);
        }

        /// <summary> Finishes the SVG file, flushes the buffer, and releases the file resources. </summary>
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
                this.UnknownLayer.Name = "Uassigned Layer " + layerID;
                return this.UnknownLayer;
            }
        }

        private void UpdateExtents(double X, double Y)
        {
            if (X < this.MinX) { this.MinX = (int)X; }
            if (X > this.MaxX) { this.MaxX = (int)X; }
            if (Y < this.MinY) { this.MinY = (int)Y; }
            if (Y > this.MaxY) { this.MaxY = (int)Y; }
        }

    }
}
