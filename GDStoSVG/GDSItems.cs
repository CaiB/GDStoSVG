using Clipper2Lib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GDStoSVG;

public class Structure
{
    public string Name { get; set; } = "Invalid Structure Name";
    public HashSet<Element>? Elements { get; set; }
    public bool IsFlattened { get; private set; } = false;

    public void OptimizeGeometry()
    {
        if (Elements == null) { return; }
        IEnumerable<IGrouping<short?, Element>> GroupedElements = this.Elements.Where(El => (El is Boundary || El is Path || El is Box || El is OptimizedGeometry)).GroupBy(El => ((LayerElement)El).Layer);
        foreach(IGrouping<short?, Element> OnLayer in GroupedElements)
        {
            List<List<PointD>> Geometry = new();
            foreach(Element Element in OnLayer)
            {
                LayerElement ElementL = (LayerElement)Element;
                if (ElementL is OptimizedGeometry Opt)
                {
                    if(Opt.Geometry == null) { continue; }
                    Geometry.AddRange(Opt.Geometry);
                }
                else
                {
                    List<PointD>? ElementGeo = ElementL.GetPolygonCoords();
                    if (ElementGeo == null) { continue; }
                    Geometry.Add(ElementGeo);
                }
            }
            
            List<List<PointD>> OptGeometry = Clipper.Union(Geometry, FillRule.NonZero);
            OptimizedGeometry NewGeo = new()
            {
                 Geometry = OptGeometry,
                 Layer = OnLayer.Key
            };
            foreach (Element El in OnLayer) { this.Elements.Remove(El); } // How come I'm allowed to modify this.Elements while enumerating?
            this.Elements.Add(NewGeo);
        }
    }

    public void FlattenAndOptimize(Transform? trans = null, List<Element>? targetList = null)
    {
        if (Elements == null || this.IsFlattened) { return; }
        trans ??= Transform.Default;
        targetList ??= new();

        List<Element> ToRemove = new();
        List<Element> ToAdd = new();

        // Deal with all non-geometry objects first
        foreach(Element SubElement in this.Elements.Where(x => x is not LayerElement))
        {
            if(SubElement is StructureRef SubStructRef)
            {
                if (!SubStructRef.Check()) { Console.WriteLine("Skipping invalid structure reference"); continue; } // StructureName, Coords are non-null after
                Structure SubStruct = GDSData.Structures[SubStructRef.StructureName!];
                SubStructRef.Transform.PositionOffset = SubStructRef.Coords![0];
                SubStruct.FlattenAndOptimize(SubStructRef.Transform, targetList);
                ToRemove.Add(SubStructRef);
                if (SubStruct.Elements == null) { continue; }
                foreach(Element El in SubStruct.Elements)
                {
                    if (El is OptimizedGeometry Geo)
                    {
                        OptimizedGeometry GeoCopy = Geo.Clone();
                        for(int i = 0; i < GeoCopy.Geometry!.Count; i++)
                        {
                            for (int j = 0; j < GeoCopy.Geometry[i].Count; j++) { GeoCopy.Geometry[i][j] = SubStructRef.Transform.ApplyTo(GeoCopy.Geometry[i][j]); }
                            if (SubStructRef.Transform.YReflect) { GeoCopy.Geometry[i].Reverse(); } // A Y-reflect means reversing the polygon direction, so reverse it back
                        }
                        ToAdd.Add(GeoCopy);
                    }
                    else if(El is LayerElement LEl)
                    {
                        LayerElement ElCopy = LEl.Clone();
                        if(ElCopy.Coords != null)
                        {
                            for (int i = 0; i < ElCopy.Coords.Length; i++) { ElCopy.Coords[i] = SubStructRef.Transform.ApplyTo(ElCopy.Coords[i]); }
                        }
                        ToAdd.Add(ElCopy);
                    }
                    else
                    {
                        Console.WriteLine("Tried to optimize unsupported child item: " + El);
                    }
                }
            }
            else if(SubElement is ArrayRef Arr)
            {
                // TODO I have no idea what to do here yet, somehow flatten this?
            }
        }

        foreach (Element RemoveMe in ToRemove) { this.Elements.Remove(RemoveMe); }
        foreach (Element AddMe in ToAdd) { this.Elements.Add(AddMe); }

        OptimizeGeometry();
        this.IsFlattened = true;
    }
}

public abstract class Element
{
    public readonly uint ID;
    public Dictionary<short, string>? Properties { get; set; }
    public bool TemplateFlag { get; set; } = false;
    public bool ExternalFlag { get; set; } = false;
    public PointD[]? Coords = null;

    public Element() { this.ID = GetNextID(); }
    public override int GetHashCode() => (int)ID;

    public abstract bool Check();

    private static uint LastState = (uint)Environment.TickCount;
    private static uint GetNextID()
    {
        uint Intermediate = (LastState) ^ (LastState >> 2) ^ (LastState >> 3) ^ (LastState >> 5);
        LastState = (LastState >> 1) | (Intermediate << 15);
        return LastState;
    }
}

public abstract class LayerElement : Element
{
    public short? Layer = null;

    public abstract List<PointD>? GetPolygonCoords();
    public abstract LayerElement Clone();
}

public class Boundary : LayerElement
{
    public short? Datatype = null;

    public override bool Check() => this.Layer != null && this.Datatype != null && this.Coords != null;
    public override List<PointD>? GetPolygonCoords() => this.Coords?.ToList();
    public override Boundary Clone() => new()
    {
        Coords = (PointD[]?)this.Coords?.Clone(),
        Datatype = this.Datatype,
        ExternalFlag = this.ExternalFlag,
        Layer = this.Layer,
        Properties = this.Properties == null ? null : new(this.Properties),
        TemplateFlag = this.TemplateFlag
    };
}

public class Path : LayerElement
{
    public short? Datatype = null;
    public short PathType = 0;
    public int Width = 0; // negative means not affected by magnification
    public int ExtensionStart, ExtensionEnd; // Only used if PathType is 4. Can be negative.

    public override bool Check() => this.Layer != null && this.Datatype != null && this.Coords != null;
    public override List<PointD>? GetPolygonCoords()
    {
        if(this.Coords == null) { return null; }
        List<List<PointD>> Input = new() { new(this.Coords) };
        EndType Ends = EndType.Butt;
        if (this.PathType == 1) { Ends = EndType.Round; }
        if (this.PathType == 2) { Ends = EndType.Square; }
        if (this.PathType == 4)
        {
            // TODO: A whole bunch of stuff here
        }
        List<List<PointD>> Output = Clipper.InflatePaths(Input, -Math.Abs(this.Width), JoinType.Square, Ends);
        if (Output.Count != 1) { throw new InvalidDataException("Trying to convert paths to polygons resulted in multiple polygons."); }
        return Output[0];
    }
    public override Path Clone() => new()
    {
        Coords = (PointD[]?)this.Coords?.Clone(),
        Datatype = this.Datatype,
        ExternalFlag = this.ExternalFlag,
        Layer = this.Layer,
        Properties = this.Properties == null ? null : new(this.Properties),
        TemplateFlag = this.TemplateFlag,
        ExtensionEnd = this.ExtensionEnd,
        ExtensionStart = this.ExtensionStart,
        PathType = this.PathType,
        Width = this.Width
    };
}

public class StructureRef : Element
{
    public string? StructureName = null;
    public Transform Transform = new();

    public override bool Check() => this.StructureName != null && this.Coords != null;
}

public class ArrayRef : Element
{
    public string? StructureName = null;
    public Transform Transform = new();
    public Tuple<short, short>? RepeatCount;

    public override bool Check() => this.StructureName != null && this.Coords != null && this.RepeatCount != null;
}

public class Text : LayerElement
{
    public int Width = 0; // negative means not affected by magnification
    public Transform Transform = new();
    public short? TextType = null;
    public byte Font = 0;
    public VerticalAlign VerticalPresentation = VerticalAlign.TOP;
    public HorizontalAlign HorizontalPresentation = HorizontalAlign.LEFT;
    public short PathType = 0;
    public string? String = null;

    public override bool Check() => this.Layer != null && this.Coords != null && this.TextType != null && this.String != null;
    public override List<PointD>? GetPolygonCoords() => null;
    public override Text Clone() => new()
    {
        Coords = (PointD[]?)this.Coords?.Clone(),
        ExternalFlag = this.ExternalFlag,
        Layer = this.Layer,
        Properties = this.Properties == null ? null : new(this.Properties),
        TemplateFlag = this.TemplateFlag,
        Font = this.Font,
        HorizontalPresentation = this.HorizontalPresentation,
        PathType = this.PathType,
        String = this.String,
        TextType = this.TextType,
        Transform = this.Transform.Clone(),
        VerticalPresentation = this.VerticalPresentation,
        Width = this.Width,
    };

    public enum VerticalAlign { TOP, MIDDLE, BOTTOM }
    public enum HorizontalAlign { LEFT, CENTER, RIGHT }
}

public class Node : LayerElement
{
    public short? NodeType = null;

    public override bool Check() => this.Layer != null && this.NodeType != null && this.Coords != null;
    public override List<PointD>? GetPolygonCoords() => this.Coords!.ToList();
    public override Node Clone() => new()
    {
        Coords = (PointD[]?)this.Coords?.Clone(),
        ExternalFlag = this.ExternalFlag,
        Layer = this.Layer,
        NodeType = this.NodeType,
        Properties = this.Properties == null ? null : new(this.Properties),
        TemplateFlag = this.TemplateFlag
    };
}

public class Box : LayerElement
{
    public short? BoxType = null;

    public override bool Check() => this.Layer != null && this.BoxType != null && this.Coords != null;
    public override List<PointD>? GetPolygonCoords() => this.Coords!.ToList();
    public override Box Clone() => new()
    {
        BoxType = this.BoxType,
        Coords = (PointD[]?)this.Coords?.Clone(),
        ExternalFlag = this.ExternalFlag,
        Layer = this.Layer,
        Properties = this.Properties == null ? null : new(this.Properties),
        TemplateFlag = this.TemplateFlag
    };
}

public class OptimizedGeometry : LayerElement
{
    public List<List<PointD>>? Geometry = null;

    [Obsolete("Don't use Coords, use Geometry instead")]
    public new readonly Point64[]? Coords = null; // We don't want to accidentally use the underlying one

    public override bool Check() => this.Layer != null && this.Geometry != null;
    public override List<PointD>? GetPolygonCoords() => null;
    public override OptimizedGeometry Clone()
    {
        List<List<PointD>>? NewGeo = this.Geometry == null ? null : new();
        if (this.Geometry != null)
        {
            for (int i = 0; i < this.Geometry.Count; i++) { NewGeo!.Add(new(this.Geometry[i])); }
        }

        return new()
        {
            ExternalFlag = this.ExternalFlag,
            Geometry = NewGeo,
            Layer = this.Layer,
            Properties = this.Properties == null ? null : new(this.Properties),
            TemplateFlag = this.TemplateFlag
        };
    }
}

public class Transform
{
    /// <summary> Reflection about the X axis (Y values affected). </summary>
    public bool YReflect = false;
    public bool MagnificationAbsolute = false;
    public bool AngleAbsolute = false;
    public double Magnification = 1.0D;
    public double Angle = 0.0D; // degrees, counterclockwise

    public PointD PositionOffset = new(0, 0);

    public static readonly Transform Default = new();

    private const double DEG_TO_RAD = (1D / 180D) * Math.PI;
    private static double RotateX(double x, double y, double angle)
    {
        if (angle == 0) { return x; }
        if (angle == 90) { return -y; }
        if (angle == 180) { return -x; }
        if (angle == 270) { return y; }
        return (x * Math.Cos(angle * DEG_TO_RAD)) - (y * Math.Sin(angle * DEG_TO_RAD));
    }
    private static double RotateY(double x, double y, double angle)
    {
        if (angle == 0) { return y; }
        if (angle == 90) { return x; }
        if (angle == 180) { return -y; }
        if (angle == 270) { return -x; }
        return (y * Math.Cos(angle * DEG_TO_RAD)) + (x * Math.Sin(angle * DEG_TO_RAD));
    }

    /// <summary> Applies a transform from a parent element onto this one to produce a compound transform. </summary>
    /// <param name="parentTrans"> The parent element's transform. </param>
    /// <returns> The new transform, with both sets of transformations applied. </returns>
    public Transform ApplyParent(Transform parentTrans)
    {
        // Rotate, then magnify, then translate
        double NewX = (RotateX(this.PositionOffset.x, this.PositionOffset.y, parentTrans.Angle) * parentTrans.Magnification) + parentTrans.PositionOffset.x;
        double RotMagY = (RotateY(this.PositionOffset.x, this.PositionOffset.y, parentTrans.Angle) * parentTrans.Magnification);
        double NewY = (parentTrans.YReflect ? -RotMagY : RotMagY) + parentTrans.PositionOffset.y;

        return new Transform
        {
            YReflect = parentTrans.YReflect ^ this.YReflect,
            MagnificationAbsolute = parentTrans.MagnificationAbsolute | this.MagnificationAbsolute,
            AngleAbsolute = parentTrans.AngleAbsolute | this.AngleAbsolute,
            Magnification = parentTrans.Magnification * this.Magnification,
            Angle = parentTrans.Angle + this.Angle,
            PositionOffset = new(NewX, NewY)
        };
    }

    public PointD ApplyTo(Point64 point)
    {
        long InX = point.X;
        long InY = this.YReflect ? -point.Y : point.Y;
        double X = RotateX(InX, InY, this.Angle);
        double Y = RotateY(InX, InY, this.Angle);
        X *= this.Magnification;
        Y *= this.Magnification;
        X += this.PositionOffset.x;
        Y += this.PositionOffset.y;
        return new(X, Y);
    }

    public PointD ApplyTo(PointD point)
    {
        double InX = point.x;
        double InY = this.YReflect ? -point.y : point.y;
        double X = RotateX(InX, InY, this.Angle);
        double Y = RotateY(InX, InY, this.Angle);
        X *= this.Magnification;
        Y *= this.Magnification;
        X += this.PositionOffset.x;
        Y += this.PositionOffset.y;
        return new(X, Y);
    }

    public Transform Clone() => new()
    {
        Angle = this.Angle,
        AngleAbsolute = this.AngleAbsolute,
        Magnification = this.Magnification,
        MagnificationAbsolute = this.MagnificationAbsolute,
        PositionOffset = this.PositionOffset,
        YReflect = this.YReflect
    };
}
