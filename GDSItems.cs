using System;
using System.Collections.Generic;
using System.Text;

namespace GDStoSVG
{
    public class Structure
    {
        public string Name { get; set; } = "Invalid Structure Name";
        public List<Element>? Elements { get; set; }
    }

    public abstract class Element
    {
        public Dictionary<short, string>? Properties { get; set; }
        public bool TemplateFlag { get; set; } = false;
        public bool ExternalFlag { get; set; } = false;
        public Tuple<int, int>[]? Coords = null;

        public abstract bool Check();
    }

    public class Boundary : Element
    {
        public short? Layer = null;
        public short? Datatype = null;

        public override bool Check() => this.Layer != null && this.Datatype != null; // TODO: Update
    }

    public class Path : Element
    {
        public short? Layer = null;
        public short? Datatype = null;
        public short PathType = 0;
        public int Width = 0;

        public override bool Check() => this.Layer != null && this.Datatype != null; // TODO: Update
    }

    public class StructureRef : Element
    {
        public string? StructureName = null;
        public bool XReflect = false;
        public bool MagnificationAbsolute = false;
        public bool AngleAbsolute = false;
        public double Magnification = 1.0D;
        public double Angle = 0.0D; // degrees, counterclockwise

        public override bool Check() => true; // TODO: Update
    }

    public class ArrayRef : Element
    {
        public string? StructureName = null;
        public bool XReflect = false;
        public bool MagnificationAbsolute = false;
        public bool AngleAbsolute = false;
        public double Magnification = 1.0D;
        public double Angle = 0.0D; // degrees, counterclockwise
        public Tuple<short, short>? RepeatCount;

        public override bool Check() => true; // TODO: Update
    }

    public class Text : Element
    {
        public short? Layer = null;
        public int Width = 0;
        public bool XReflect = false;
        public bool MagnificationAbsolute = false;
        public bool AngleAbsolute = false;
        public double Magnification = 1.0D;
        public double Angle = 0.0D; // degrees, counterclockwise

        public override bool Check() => true; // TODO: Update
    }

    public class Node : Element
    {
        public short? Layer = null;
        public short? NodeType = null;

        public override bool Check() => true; // TODO: Update
    }

    public class Box : Element
    {
        public short? Layer = null;
        public short? BoxType = null;

        public override bool Check() => true; // TODO: Update
    }
}
