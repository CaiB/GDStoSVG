using Clipper2Lib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GDStoSVG;

public class Structure
{
    public string Name { get; set; } = "Invalid Structure Name";
    public List<Element>? Elements { get; set; }

    public void OptimizeGeometry()
    {
        if (Elements == null) { return; }
        IEnumerable<IGrouping<short?, Element>> GroupedElements = this.Elements.Where(El => (El is Boundary || El is Path || El is Box)).GroupBy(El => ((LayerElement)El).Layer);
        foreach(IGrouping<short?, Element> OnLayer in GroupedElements)
        {
            List<List<Point64>> Geometry = OnLayer.Select(El => ((LayerElement)El).GetPolygonCoords()).Where(x => x is not null).ToList();
            List<List<Point64>> OptGeometry = Clipper.Union(Geometry, FillRule.NonZero);
            OptimizedGeometry NewGeo = new()
            {
                 Geometry = OptGeometry,
                 Layer = OnLayer.Key
            };
            this.Elements.RemoveAll(El => OnLayer.Contains(El));
            this.Elements.Add(NewGeo);
        }
    }
}

public abstract class Element
{
    public Dictionary<short, string>? Properties { get; set; }
    public bool TemplateFlag { get; set; } = false;
    public bool ExternalFlag { get; set; } = false;
    public Point64[]? Coords = null;

    public abstract bool Check();
}

public abstract class LayerElement : Element
{
    public short? Layer = null;

    public abstract List<Point64>? GetPolygonCoords();
}

public class Boundary : LayerElement
{
    public short? Datatype = null;

    public override bool Check() => this.Layer != null && this.Datatype != null && this.Coords != null;
    public override List<Point64>? GetPolygonCoords() => this.Coords?.ToList();
}

public class Path : LayerElement
{
    public short? Datatype = null;
    public short PathType = 0;
    public int Width = 0; // negative means not affected by magnification

    public override bool Check() => this.Layer != null && this.Datatype != null && this.Coords != null;
    public override List<Point64>? GetPolygonCoords() => null; // TODO: Implement this, it's going to be not so easy though
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
    public override List<Point64>? GetPolygonCoords() => null;

    public enum VerticalAlign { TOP, MIDDLE, BOTTOM }
    public enum HorizontalAlign { LEFT, CENTER, RIGHT }
}

public class Node : LayerElement
{
    public short? NodeType = null;

    public override bool Check() => this.Layer != null && this.NodeType != null && this.Coords != null;
    public override List<Point64>? GetPolygonCoords() => this.Coords!.ToList();
}

public class Box : LayerElement
{
    public short? BoxType = null;

    public override bool Check() => this.Layer != null && this.BoxType != null && this.Coords != null;
    public override List<Point64>? GetPolygonCoords() => this.Coords!.ToList();
}

public class OptimizedGeometry : LayerElement
{
    public List<List<Point64>>? Geometry = null;

    [Obsolete("Don't use Coords, use Geometry instead")]
    public new readonly Point64[]? Coords = null; // We don't want to accidentally use the underlying one

    public override bool Check() => this.Layer != null && this.Geometry != null;
    public override List<Point64>? GetPolygonCoords() => null;
}

public class Transform
{
    /// <summary> Reflection about the X axis (Y values affected). </summary>
    public bool YReflect = false;
    public bool MagnificationAbsolute = false;
    public bool AngleAbsolute = false;
    public double Magnification = 1.0D;
    public double Angle = 0.0D; // degrees, counterclockwise

    public Point64 PositionOffset = new(0, 0);

    public static readonly Transform Default = new();

    /// <summary> Applies a transform from a parent element onto this one to produce a compound transform. </summary>
    /// <param name="trans"> The parent element's transform. </param>
    /// <returns> The new transform, with both sets of transformations applied. </returns>
    public Transform ApplyParent(Transform trans)
    {
        long NewX = trans.PositionOffset.X + this.PositionOffset.X;
        long NewY = trans.PositionOffset.Y + (trans.YReflect ? -this.PositionOffset.Y : this.PositionOffset.Y);
        return new Transform
        {
            YReflect = trans.YReflect ^ this.YReflect,
            MagnificationAbsolute = trans.MagnificationAbsolute | this.MagnificationAbsolute,
            AngleAbsolute = trans.AngleAbsolute | this.AngleAbsolute,
            Magnification = trans.Magnification * this.Magnification,
            Angle = trans.Angle + this.Angle,
            PositionOffset = new(NewX, NewY)
        };
    }
}
