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

        public override bool Check() => this.Layer != null && this.Datatype != null; // TODO: Update
    }

    public class StructureRef : Element
    {

        public override bool Check() => true; // TODO: Update
    }

    public class ArrayRef : Element
    {
        public override bool Check() => true; // TODO: Update
    }

    public class Text : Element
    {
        public short? Layer = null;
        public override bool Check() => true; // TODO: Update
    }

    public class Node : Element
    {
        public short? Layer = null;
        public override bool Check() => true; // TODO: Update
    }

    public class Box : Element
    {
        public short? Layer = null;
        public override bool Check() => true; // TODO: Update
    }
}
