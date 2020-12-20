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

        public abstract bool Check();
    }

    public class Boundary : Element
    {
        public short? Layer = null;
        public short? Datatype = null;

        public override bool Check() => this.Layer != null && this.Datatype != null;
    }

    public class Path : Element
    {
        public short? Layer = null;
        public short? Datatype = null;

        public override bool Check() => this.Layer != null && this.Datatype != null;
    }

    public class StructureRef : Element
    {

        public override bool Check() => true;
    }

    public class ArrayRef : Element
    {
        public override bool Check() => true;
    }

    public class Text : Element
    {
        public override bool Check() => true;
    }

    public class Node : Element
    {
        public override bool Check() => true;
    }

    public class Box : Element
    {
        public override bool Check() => true;
    }
}
