using Clipper2Lib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GDStoSVG;

public class GDSReader
{
    /// <summary> Whether the platform we are running on is little-endian (true in most cases). </summary>
    private readonly bool IsLE;

    /// <summary> A list of the structures present in the file. </summary>
    private readonly List<Structure> Structures = new();

    public GDSReader() { this.IsLE = BitConverter.IsLittleEndian; }

    /// <summary> Reads all data from the given GDS file, and stores the results in <see cref="GDSData"/> fields. </summary>
    /// <param name="fileName"> The name of the file to read from. </param>
    public void ReadFile(string fileName)
    {
        using (BinaryReader Reader = new(File.Open(fileName, FileMode.Open)))
        {
            long FileLength = Reader.BaseStream.Length;
            while (Reader.BaseStream.Position < FileLength)
            {
                ushort Length = ReadUShort(Reader);
                RecordType Type = (RecordType)ReadUShort(Reader);
                int DataLength = Length - 4; // Remove 4 bytes for the header for data length
                byte[]? Data = (DataLength <= 0) ? null : Reader.ReadBytes(DataLength);
                bool StopReading = ReadRecord(Type, Data);
                if (StopReading) { break; }
            }
        }
        GDSData.LastStructure = this.Structures[^1];

        Console.WriteLine("Found {0} units in GDS file{1}", this.Structures.Count, (Program.Info ? ":" : "."));
        foreach (Structure Str in this.Structures)
        {
            int ElementCount = Str.Elements?.Count ?? 0;
            if (Program.Info) { Console.WriteLine("  Unit \"{0}\" ({1} object{2}){3}", Str.Name, ElementCount, (ElementCount != 1 ? "s" : ""), (Str == GDSData.LastStructure ? " -> Top-level unit" : "")); }
            GDSData.Structures.Add(Str.Name, Str);
        }
    }

    /// <summary> Used while parsing the file. Keeps the incomplete structure that data is being read about currently. </summary>
    private Structure? CurrentStructure = null;

    /// <summary> Used while parsing the file. Keeps the incomplete element that data is being read about currently. </summary>
    private Element? CurrentElement = null;

    /// <summary> Used while parsing the file. Keeps the property key for which a value still needs to be read. </summary>
    private short? CurrentProperty = null;

    /// <summary> Parses one record from the byte stream. </summary>
    /// <param name="type"> The record type to parse. </param>
    /// <param name="data"> The data associated with the record. </param>
    /// <returns> Whether this is the end of the file, and we should stop parsing. </returns>
    private bool ReadRecord(RecordType type, byte[]? data)
    {
        //Console.WriteLine("Reading " + type.ToString());
        Type ElementType;
        switch (type) // List sorted somewhat in order of expected file structure.
        {
            case RecordType.HEADER:
                if (data == null || data.Length < 2) { throw new InvalidDataException("Header had insufficient data"); }
                if(Program.Info) { Console.WriteLine(string.Format("File version is 0x{0:X2}{1:X2}", data[0], data[1])); }
                break;
            case RecordType.BGNLIB:
                // TODO: Read last modified/accessed times
                break;
            case RecordType.LIBDIRSIZE: // optional
            case RecordType.SRFNAME: // optional
            case RecordType.LIBSECUR: // optional
                break;
            case RecordType.LIBNAME:
                if (data == null || data.Length == 0) { throw new InvalidDataException("Library name had no data"); }
                string LibName = ParseString(data, 0, data.Length);
                GDSData.LibraryName = LibName;
                break;
            case RecordType.REFLIBS: // optional
                // TODO: Read this?
                break;
            case RecordType.FONTS: // optional
            case RecordType.ATTRTABLE: // optional
            case RecordType.GENERATIONS: // optional
                break;
            case RecordType.FORMAT:
                break;
            case RecordType.MASK: // optional
            case RecordType.ENDMASKS: // optional
                break;
            case RecordType.UNITS:
                // TODO: Read unit data
                break;
            case RecordType.ENDLIB:
                return true;
            case RecordType.BGNSTR:
                if (this.CurrentStructure != null) { throw new InvalidDataException("Structure started before finishing previous one."); }
                this.CurrentStructure = new Structure();
                // TODO: Read last modify/access time
                break;
            case RecordType.STRNAME:
                if (this.CurrentStructure == null) { throw new InvalidDataException("Structure name found outside of a structure."); }
                if (data == null || data.Length == 0) { throw new InvalidDataException("Structure name had no data"); }
                this.CurrentStructure.Name = ParseString(data, 0, data.Length);
                if (Program.Debug) { Console.WriteLine("Reading structure \"{0}\"", this.CurrentStructure.Name); }
                break;
            case RecordType.STRCLASS: // optional
                break;
            case RecordType.ENDSTR:
                if (this.CurrentStructure == null) { throw new InvalidDataException("Structure end found outside of a structure."); }
                this.Structures.Add(this.CurrentStructure);
                this.CurrentStructure = null;
                break;
            case RecordType.BOUNDARY:
                if (this.CurrentElement != null) { throw new InvalidDataException("Element started before finishing previous one."); }
                this.CurrentElement = new Boundary();
                break;
            case RecordType.PATH:
                if (this.CurrentElement != null) { throw new InvalidDataException("Element started before finishing previous one."); }
                this.CurrentElement = new Path();
                break;
            case RecordType.SREF:
                if (this.CurrentElement != null) { throw new InvalidDataException("Element started before finishing previous one."); }
                this.CurrentElement = new StructureRef();
                break;
            case RecordType.AREF:
                if (this.CurrentElement != null) { throw new InvalidDataException("Element started before finishing previous one."); }
                this.CurrentElement = new ArrayRef();
                break;
            case RecordType.TEXT:
                if (this.CurrentElement != null) { throw new InvalidDataException("Element started before finishing previous one."); }
                this.CurrentElement = new Text();
                break;
            case RecordType.NODE:
                if (this.CurrentElement != null) { throw new InvalidDataException("Element started before finishing previous one."); }
                this.CurrentElement = new Node();
                break;
            case RecordType.BOX:
                if (this.CurrentElement != null) { throw new InvalidDataException("Element started before finishing previous one."); }
                this.CurrentElement = new Box();
                break;
            case RecordType.ELFLAGS:
                if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign element flags with no element."); }
                if (data == null || data.Length < 2) { throw new InvalidDataException("Element flags had insufficient data"); }
                ushort FlagData = (ushort)ParseShort(data, 0);
                this.CurrentElement.TemplateFlag = (FlagData & 0b1) == 0b1;
                this.CurrentElement.ExternalFlag = (FlagData & 0b10) == 0b10;
                break;
            case RecordType.PLEX:
                // TODO: Read plex
                if (data == null || data.Length < 4) { throw new InvalidDataException("Plex had insufficient data"); }
                Console.WriteLine(string.Format("Plex ID is 0x{0:X2}{1:X2}{2:X2}{3:X2}", data[0], data[1], data[2], data[3]));
                break;
            case RecordType.LAYER:
                if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign layer with no element."); }
                if (data == null || data.Length < 2) { throw new InvalidDataException("Layer assignment had insufficient data"); }
                short Layer = ParseShort(data, 0);
                ElementType = this.CurrentElement.GetType();
                if (ElementType == typeof(Boundary)) { ((Boundary)this.CurrentElement).Layer = Layer; }
                else if (ElementType == typeof(Path)) { ((Path)this.CurrentElement).Layer = Layer; }
                else if (ElementType == typeof(Text)) { ((Text)this.CurrentElement).Layer = Layer; }
                else if (ElementType == typeof(Node)) { ((Node)this.CurrentElement).Layer = Layer; }
                else if (ElementType == typeof(Box)) { ((Box)this.CurrentElement).Layer = Layer; }
                else { throw new InvalidOperationException("Tried to assign layer to element which cannot accept layer data."); }
                break;
            case RecordType.XY:
                if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign coordinates with no element."); }
                if (data == null || data.Length == 0) { throw new InvalidDataException("Coordinate assignment had insufficient data"); }
                if (data.Length % 8 != 0) { throw new InvalidDataException("XY coordinates had uneven number of elements"); }
                PointD[] Coords = new PointD[data.Length / 8];
                for(int i = 0; i < Coords.Length; i++)
                {
                    Coords[i] = new(ParseInt(data, i * 8), ParseInt(data, (i * 8) + 4));
                }
                this.CurrentElement.Coords = Coords;
                break;
            case RecordType.DATATYPE:
                if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign datatype with no element."); }
                if (data == null || data.Length < 2) { throw new InvalidDataException("Datatype assignment had insufficient data"); }
                short Datatype = ParseShort(data, 0);
                ElementType = this.CurrentElement.GetType();
                if (ElementType == typeof(Boundary)) { ((Boundary)this.CurrentElement).Datatype = Datatype; }
                else if (ElementType == typeof(Path)) { ((Path)this.CurrentElement).Datatype = Datatype; }
                else { throw new InvalidOperationException("Tried to assign datatype to element which cannot accept datatype info."); }
                break;
            case RecordType.PATHTYPE:
                if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign path type with no element."); }
                if (data == null || data.Length < 2) { throw new InvalidDataException("Path type assignment had insufficient data"); }
                short PathType = ParseShort(data, 0);
                if (this.CurrentElement is Path Pth) { Pth.PathType = PathType; }
                else if (this.CurrentElement is Text Txt2) { Txt2.PathType = PathType; }
                else { throw new InvalidOperationException("Tried to assign path type to element that does not support path type."); }
                break;
            case RecordType.WIDTH:
                if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign width with no element."); }
                if (data == null || data.Length < 4) { throw new InvalidDataException("Width assignment had insufficient data"); }
                int Width = ParseInt(data, 0);
                ElementType = this.CurrentElement.GetType();
                if (ElementType == typeof(Path)) { ((Path)this.CurrentElement).Width = Width; }
                else if (ElementType == typeof(Text)) { ((Text)this.CurrentElement).Width = Width; }
                else { throw new InvalidOperationException("Tried to assign width to element which cannot accept width data."); }
                break;
            case RecordType.BGNEXTN: // optional; Specific to CustomPlus software.
                if (this.CurrentElement is not Path Pth2) { throw new InvalidDataException("Trying to assign path beginning extension without active path element."); }
                if (data == null || data.Length < 4) { throw new InvalidDataException("Path beginning extension had insufficient data"); }
                int PathExtensionStart = ParseInt(data, 0);
                Pth2.ExtensionStart = PathExtensionStart;
                break;
            case RecordType.ENDEXTN:
                if (this.CurrentElement is not Path Pth3) { throw new InvalidDataException("Trying to assign path end extension without active path element."); }
                if (data == null || data.Length < 4) { throw new InvalidDataException("Path end extension had insufficient data"); }
                int PathExtensionEnd = ParseInt(data, 0);
                Pth3.ExtensionEnd = PathExtensionEnd;
                break;
            case RecordType.SNAME:
                if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign structure reference with no element."); }
                if (data == null || data.Length == 0) { throw new InvalidDataException("Structure reference name had no data"); }
                string StructRefName = ParseString(data, 0, data.Length);
                ElementType = this.CurrentElement.GetType();
                if (ElementType == typeof(StructureRef)) { ((StructureRef)this.CurrentElement).StructureName = StructRefName; }
                else if (ElementType == typeof(ArrayRef)) { ((ArrayRef)this.CurrentElement).StructureName = StructRefName; }
                else { throw new InvalidOperationException("Tried to assign structure reference name to non-reference element."); }
                break;
            case RecordType.STRANS:
                if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign structure transform with no element."); }
                if (data == null || data.Length < 2) { throw new InvalidDataException("Element flags had insufficient data"); }
                ushort TransformData = (ushort)ParseShort(data, 0);
                bool Reflect = (TransformData & 0x8000) == 0x8000;
                bool AbsMag = (TransformData & 0x0004) == 0x0004;
                bool AbsAng = (TransformData & 0x0002) == 0x0002;
                if (this.CurrentElement is StructureRef SRef)
                {
                    SRef.Transform.YReflect = Reflect;
                    SRef.Transform.MagnificationAbsolute = AbsMag;
                    SRef.Transform.AngleAbsolute = AbsAng;
                }
                else if (this.CurrentElement is ArrayRef ARef)
                {
                    ARef.Transform.YReflect = Reflect;
                    ARef.Transform.MagnificationAbsolute = AbsMag;
                    ARef.Transform.AngleAbsolute = AbsAng;
                }
                else if (this.CurrentElement is Text Txt)
                {
                    Txt.Transform.YReflect = Reflect;
                    Txt.Transform.MagnificationAbsolute = AbsMag;
                    Txt.Transform.AngleAbsolute = AbsAng;
                }
                else { throw new InvalidOperationException("Tried to assign structure transform properties to non-reference element."); }
                break;
            case RecordType.MAG:
                if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign magnification with no element."); }
                if (data == null || data.Length < 8) { throw new InvalidDataException("Element magnification had insufficient data"); }
                double Mag = ParseDouble(data, 0);
                if (this.CurrentElement is StructureRef StRef) { StRef.Transform.Magnification = Mag; }
                else if (this.CurrentElement is ArrayRef ArRef) { ArRef.Transform.Magnification = Mag; }
                else if (this.CurrentElement is Text Txt2) { Txt2.Transform.Magnification = Mag; }
                else { throw new InvalidOperationException("Tried to assign magnification to element that cannot take magnification parameter."); }
                break;
            case RecordType.ANGLE:
                if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign angle with no element."); }
                if (data == null || data.Length < 8) { throw new InvalidDataException("Element angle had insufficient data"); }
                double Angle = ParseDouble(data, 0);
                if (this.CurrentElement is StructureRef StRef2) { StRef2.Transform.Angle = Angle; }
                else if (this.CurrentElement is ArrayRef ArRef2) { ArRef2.Transform.Angle = Angle; }
                else if (this.CurrentElement is Text Txt3) { Txt3.Transform.Angle = Angle; }
                else { throw new InvalidOperationException("Tried to assign angle to element that cannot take angle parameter."); }
                break;
            case RecordType.COLROW:
                if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign col/row count with no element."); }
                if (data == null || data.Length < 4) { throw new InvalidDataException("Col/row count had insufficient data"); }
                if (this.CurrentElement.GetType() != typeof(ArrayRef)) { throw new InvalidOperationException("Cannot assign col/row count to non-array element."); }
                short Col = ParseShort(data, 0);
                short Row = ParseShort(data, 2);
                ((ArrayRef)this.CurrentElement).RepeatCount = new Tuple<short, short>(Col, Row);
                break;
            case RecordType.NODETYPE:
                if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign node type with no element."); }
                if (data == null || data.Length < 2) { throw new InvalidDataException("Node type had insufficient data"); }
                if (this.CurrentElement.GetType() != typeof(Node)) { throw new InvalidOperationException("Cannot assign node type to non-node element."); }
                ((Node)this.CurrentElement).NodeType = ParseShort(data, 0);
                break;
            case RecordType.BOXTYPE:
                if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign box type with no element."); }
                if (data == null || data.Length < 2) { throw new InvalidDataException("Box type had insufficient data"); }
                if (this.CurrentElement.GetType() != typeof(Box)) { throw new InvalidOperationException("Cannot assign box type to non-box element."); }
                ((Box)this.CurrentElement).BoxType = ParseShort(data, 0);
                break;
            case RecordType.TEXTTYPE:
                if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign text type with no element."); }
                if (data == null || data.Length < 2) { throw new InvalidDataException("Text type had insufficient data"); }
                if (this.CurrentElement.GetType() != typeof(Text)) { throw new InvalidOperationException("Cannot assign text type to non-text element."); }
                ((Text)this.CurrentElement).TextType = ParseShort(data, 0);
                break;
            case RecordType.PRESENTATION:
                if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign presentation with no element."); }
                if (data == null || data.Length < 2) { throw new InvalidDataException("Presentation had insufficient data"); }
                if (this.CurrentElement.GetType() != typeof(Text)) { throw new InvalidOperationException("Cannot assign presentation to non-text element."); }
                Text TextElement = (Text)this.CurrentElement;
                ushort PresData = (ushort)ParseShort(data, 0);
                switch(PresData & 0b11)
                {
                    case 0b00: TextElement.HorizontalPresentation = Text.HorizontalAlign.LEFT; break;
                    case 0b01: TextElement.HorizontalPresentation = Text.HorizontalAlign.CENTER; break;
                    case 0b10: TextElement.HorizontalPresentation = Text.HorizontalAlign.RIGHT; break;
                }
                switch((PresData >> 2) & 0b11)
                {
                    case 0b00: TextElement.VerticalPresentation = Text.VerticalAlign.TOP; break;
                    case 0b01: TextElement.VerticalPresentation = Text.VerticalAlign.MIDDLE; break;
                    case 0b10: TextElement.VerticalPresentation = Text.VerticalAlign.BOTTOM; break;
                }
                TextElement.Font = (byte)((PresData >> 4) & 0b11);
                break;
            case RecordType.STRING:
                if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign string with no element."); }
                if (data == null || data.Length == 0) { throw new InvalidDataException("String had no data"); }
                if (this.CurrentElement.GetType() != typeof(Text)) { throw new InvalidOperationException("Cannot assign string to non-text element."); }
                string TextStr = ParseString(data, 0, data.Length);
                ((Text)this.CurrentElement).String = TextStr;
                break;
            case RecordType.PROPATTR:
                if (this.CurrentProperty != null) { throw new InvalidDataException("New property starting before previous one had value assigned."); }
                if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign property with no element to attach to."); }
                if (data == null || data.Length < 2) { throw new InvalidDataException("Property key had insufficient data"); }
                this.CurrentProperty = ParseShort(data, 0);
                break;
            case RecordType.PROPVALUE:
                if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign property with no element to attach to."); }
                if (this.CurrentProperty == null) { throw new InvalidDataException("Trying to assign property data without key."); }
                if (data == null || data.Length == 0) { throw new InvalidDataException("Property value had insufficient data"); }
                this.CurrentElement.Properties ??= new();
                string Value = ParseString(data, 0, data.Length);
                this.CurrentElement.Properties.Add((short)this.CurrentProperty, Value);
                this.CurrentProperty = null;
                break;
            case RecordType.ENDEL:
                if (this.CurrentStructure == null) { throw new InvalidDataException("Element ended outside of structure."); }
                if (this.CurrentElement == null) { throw new InvalidDataException("Element ending before starting."); }
                this.CurrentStructure.Elements ??= new();
                if (!this.CurrentElement.Check()) { Console.WriteLine("Element does not have all required data present."); }
                if (this.CurrentElement is not Text || !Program.IgnoreAllText) { this.CurrentStructure.Elements.Add(this.CurrentElement); }
                this.CurrentElement = null;
                break;
        }
        return false;
    }

    /// <summary> Reads an unsigned 16-bit integer from the datastream, in big-endian format. </summary>
    /// <param name="reader"> The data source to read from. </param>
    /// <returns> The data read. </returns>
    private ushort ReadUShort(BinaryReader reader) => this.IsLE ? (ushort)((reader.ReadByte() << 8) | reader.ReadByte()) : reader.ReadUInt16();

    /// <summary> Reads a string from a record's data. </summary>
    /// <param name="data"> The byte array to read data from. </param>
    /// <param name="position"> Where to start reading the string in the data array. </param>
    /// <param name="length"> The expected string length. Trailing 0x00 characters are trimmed, so final length may be less. </param>
    /// <returns> The parsed ASCII string. </returns>
    private string ParseString(byte[] data, int position, int length)
    {
        while (data[position + length - 1] == 0x00) { length--; } // Remove trailing 0x00 characters.
        return Encoding.ASCII.GetString(data, position, length);
    }

    /// <summary> Reads a <see cref="short"/> from a record's data. </summary>
    /// <param name="data"> The byte array to read data from. </param>
    /// <param name="position"> Where to start reading. </param>
    /// <returns> The parsed short. </returns>
    private short ParseShort(byte[] data, int position)
    {
        if (this.IsLE) { return (short)((data[position] << 8) | data[position + 1]); }
        else { return (short)(data[position] | (data[position + 1] << 8)); }
    }

    /// <summary> Reads an <see cref="int"/> from a record's data. </summary>
    /// <param name="data"> The byte array to read data from. </param>
    /// <param name="position"> Where to start reading. </param>
    /// <returns> The parsed int. </returns>
    private int ParseInt(byte[] data, int position)
    {
        if (this.IsLE) { return ((data[position] << 24) | (data[position + 1] << 16) | (data[position + 2] << 8) | data[position + 3]); }
        else { return (data[position] | (data[position + 1] << 8) | (data[position + 2] << 16) | (data[position + 3] << 24)); }
    }

    /// <summary> Reads an <see cref="double"/> from a record's data. </summary>
    /// <param name="data"> The byte array to read data from. </param>
    /// <param name="position"> Where to start reading. </param>
    /// <returns> The parsed double. </returns>
    private double ParseDouble(byte[] data, int position)
    {
        byte[] DoubleData = new byte[8];
        Array.Copy(data, position, DoubleData, 0, 8);
        if (this.IsLE) { Array.Reverse(DoubleData); }
        bool Negative = (DoubleData[7] >> 7) == 1;
        byte Exponent = (byte)(DoubleData[7] & 0b0111_1111);
        ulong Mantissa = ((ulong)DoubleData[6] << 48) |
                         ((ulong)DoubleData[5] << 40) |
                         ((ulong)DoubleData[4] << 32) |
                         ((ulong)DoubleData[3] << 24) |
                         ((ulong)DoubleData[2] << 16) |
                         ((ulong)DoubleData[1] << 8) |
                         DoubleData[0];
        double Value = ((double)Mantissa / (1UL << 56)) * Math.Pow(16, Exponent - 64);
        return Negative ? -Value : Value;
    }

    /// <summary> Checks a standard set of 8B values to see if the <see cref="double"/> parsing is working correctly. </summary>
    public void TestDoubleParse()
    {
        Console.WriteLine("Testing double parsing:");
        TestDoubleParse(0b01000001_00010000_00000000_00000000_00000000_00000000_00000000_00000000, 1D);
        TestDoubleParse(0b01000001_00100000_00000000_00000000_00000000_00000000_00000000_00000000, 2D);
        TestDoubleParse(0b01000001_00110000_00000000_00000000_00000000_00000000_00000000_00000000, 3D);
        TestDoubleParse(0b11000001_00010000_00000000_00000000_00000000_00000000_00000000_00000000, -1D);
        TestDoubleParse(0b11000001_00100000_00000000_00000000_00000000_00000000_00000000_00000000, -2D);
        TestDoubleParse(0b11000001_00110000_00000000_00000000_00000000_00000000_00000000_00000000, -3D);
        TestDoubleParse(0b01000000_10000000_00000000_00000000_00000000_00000000_00000000_00000000, 0.5D);
        TestDoubleParse(0b01000000_10011001_10011001_10011001_10011001_10011001_10011001_10011001, 0.6D);
        TestDoubleParse(0b01000000_10110011_00110011_00110011_00110011_00110011_00110011_00110011, 0.7D);
        TestDoubleParse(0b01000001_00011000_00000000_00000000_00000000_00000000_00000000_00000000, 1.5D);
        TestDoubleParse(0b01000001_00011001_10011001_10011001_10011001_10011001_10011001_10011001, 1.6D);
        TestDoubleParse(0b01000001_00011011_00110011_00110011_00110011_00110011_00110011_00110011, 1.7D);
        TestDoubleParse(0b00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000000, 0D);
        TestDoubleParse(0b01000001_10100000_00000000_00000000_00000000_00000000_00000000_00000000, 10D);
        TestDoubleParse(0b01000010_01100100_00000000_00000000_00000000_00000000_00000000_00000000, 100D);
        TestDoubleParse(0b01000011_00111110_10000000_00000000_00000000_00000000_00000000_00000000, 1_000D);
        TestDoubleParse(0b01000100_00100111_00010000_00000000_00000000_00000000_00000000_00000000, 10_000D);
        TestDoubleParse(0b01000101_00011000_01101010_00000000_00000000_00000000_00000000_00000000, 100_000D);
    }

    /// <summary> Converts a single input to <see cref="double"/> representation, and compares it against expected output. </summary>
    /// <param name="data"> The input data to try converting. </param>
    /// <param name="expected"> The expected output data. </param>
    /// <returns> Whether the result was within tolerance. </returns>
    private bool TestDoubleParse(ulong data, double expected)
    {
        byte[] ByteData = BitConverter.GetBytes(data);
        Array.Reverse(ByteData); // Simulate file formatting.
        double Result = ParseDouble(ByteData, 0);
        bool Success = Math.Abs(Result - expected) <= Math.Abs(Result) / 10000F;
        Console.ForegroundColor = Success ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine("Input: 0x{0:X16}, calculated {1}, expected {2} -> Pass: {3}", data, Result, expected, Success);
        Console.ResetColor();
        return Success;
    }

    /// <summary> List of the different kinds of records that can be found in the GDS file. </summary>
    private enum RecordType : ushort
    {
        /// <summary> Stream version number </summary>
        /// <remarks> Data: <see cref="short"/> </remarks>
        HEADER = 0x0002,

        /// <summary> Beginning of library </summary>
        /// <remarks> Data: <see cref="short"/> </remarks>
        BGNLIB = 0x0102,

        /// <summary> Name of library </summary>
        /// <remarks> Data: <see cref="string"/> </remarks>
        LIBNAME = 0x0206,

        /// <summary> User and database unit definition </summary>
        /// <remarks> Data: <see cref="double"/> </remarks>
        UNITS = 0x0305,

        /// <summary> End of library </summary>
        /// <remarks> Data: None </remarks>
        ENDLIB = 0x0400,

        /// <summary> Beginning of structure, including creation and modification time </summary>
        /// <remarks> Data: <see cref="short"/> </remarks>
        BGNSTR = 0x0502,

        /// <summary> Name of structure </summary>
        /// <remarks> Data: <see cref="string"/> </remarks>
        STRNAME = 0x0606,

        /// <summary> End of structure </summary>
        /// <remarks> Data: None </remarks>
        ENDSTR = 0x0700,

        /// <summary> Beginning of boundary element </summary>
        /// <remarks> Data: None </remarks>
        BOUNDARY = 0x0800,

        /// <summary> Beginning of path element </summary>
        /// <remarks> Data: None </remarks>
        PATH = 0x0900,

        /// <summary> Beginning of structure reference element </summary>
        /// <remarks> Data: None </remarks>
        SREF = 0x0A00,

        /// <summary> Beginning of array reference element </summary>
        /// <remarks> Data: None </remarks>
        AREF = 0x0B00,

        /// <summary> Beginning of text element </summary>
        /// <remarks> Data: None </remarks>
        TEXT = 0x0C00,

        /// <summary> Layer ID of element </summary>
        /// <remarks> Data: <see cref="short"/> </remarks>
        LAYER = 0x0D02,

        /// <summary> Datatype ID of element </summary>
        /// <remarks> Data: <see cref="short"/> </remarks>
        DATATYPE = 0x0E02,

        /// <summary> Width of element in database units </summary>
        /// <remarks> Data: <see cref="int"/> </remarks>
        WIDTH = 0x0F03,

        /// <summary> List of X-Y coordinates in database units </summary>
        /// <remarks> Data: <see cref="int"/> </remarks>
        XY = 0x1003,

        /// <summary> End of element </summary>
        /// <remarks> Data: None </remarks>
        ENDEL = 0x1100,

        /// <summary> Name of structure reference </summary>
        /// <remarks> Data: <see cref="string"/> </remarks>
        SNAME = 0x1206,

        /// <summary> Number of columns and rows in array reference </summary>
        /// <remarks> Data: <see cref="short"/> </remarks>
        COLROW = 0x1302,

        /// <summary> Beginning of node element </summary>
        /// <remarks> Data: None </remarks>
        NODE = 0x1500,

        /// <summary> Text-type number </summary>
        /// <remarks> Data: <see cref="short"/> </remarks>
        TEXTTYPE = 0x1602,

        /// <summary> Text presentation / font </summary>
        /// <remarks> Data: <see cref="bool[]"/> </remarks>
        PRESENTATION = 0x1701,

        /// <summary> ASCII string for text element </summary>
        /// <remarks> Data: <see cref="string"/> </remarks>
        STRING = 0x1906,

        /// <summary> Array reference, structure reference, and text transform flags </summary>
        /// <remarks> Data: <see cref="bool[]"/> </remarks>
        STRANS = 0x1A01,

        /// <summary> Magnification factor for text and references </summary>
        /// <remarks> Data: <see cref="double"/> </remarks>
        MAG = 0x1B05,

        /// <summary> Rotation angle for text and references </summary>
        /// <remarks> Data: <see cref="double"/> </remarks>
        ANGLE = 0x1C05,

        /// <summary> Name of referenced libraries </summary>
        /// <remarks> Data: <see cref="string"/> </remarks>
        REFLIBS = 0x1F06,

        /// <summary> Name of text fonts definition files </summary>
        /// <remarks> Data: <see cref="string"/> </remarks>
        FONTS = 0x2006,

        /// <summary> Path element endcap type </summary>
        /// <remarks> Data: <see cref="short"/> </remarks>
        PATHTYPE = 0x2102,

        /// <summary> Number of deleted structure </summary>
        /// <remarks> Data: <see cref="short"/> </remarks>
        GENERATIONS = 0x2202,

        /// <summary> Attribute table, used in combination with element properties </summary>
        /// <remarks> Data: <see cref="string"/> </remarks>
        ATTRTABLE = 0x2306,

        /// <summary> Template data </summary>
        /// <remarks> Data: <see cref="bool[]"/> </remarks>
        ELFLAGS = 0x2601,

        /// <summary> Node type number for <see cref="RecordType.NODE"/> element </summary>
        /// <remarks> Data: <see cref="short"/> </remarks>
        NODETYPE = 0x2A02,

        /// <summary> Attribute number </summary>
        /// <remarks> Data: <see cref="short"/> </remarks>
        PROPATTR = 0x2B02,

        /// <summary> Attribute name </summary>
        /// <remarks> Data: <see cref="string"/> </remarks>
        PROPVALUE = 0x2C06,

        /// <summary> Beginning of box element </summary>
        /// <remarks> Data: None </remarks>
        BOX = 0x2D00,

        /// <summary> Boxtype of box element </summary>
        /// <remarks> Data: <see cref="short"/> </remarks>
        BOXTYPE = 0x2E02,

        /// <summary> Plex number </summary>
        /// <remarks> Data: <see cref="int"/> </remarks>
        PLEX = 0x2F03,

        /// <summary> Path beginning extension length, specific to CustomPlus software. </summary>
        /// <remarks> Data: <see cref="int"/> </remarks>
        BGNEXTN = 0x3003,

        /// <summary> Path end extension length, specific to CustomPlus software. </summary>
        /// <remarks> Data: <see cref="int"/> </remarks>
        ENDEXTN = 0x3103,

        /// <summary> Tape number </summary>
        /// <remarks> Data: <see cref="short"/> </remarks>
        TAPENUM = 0x3202,

        /// <summary> Tape code </summary>
        /// <remarks> Data: <see cref="short"/> </remarks>
        TAPECODE = 0x3302,

        /// <summary> Calma CAD software internal use </summary>
        /// <remarks> Data: <see cref="bool[]"/> </remarks>
        STRCLASS = 0x3401,

        /// <summary> Format type </summary>
        /// <remarks> Data: <see cref="short"/> </remarks>
        FORMAT = 0x3602,

        /// <summary> List of layers </summary>
        /// <remarks> Data: <see cref="string"/> </remarks>
        MASK = 0x3706,

        /// <summary> End of <see cref="RecordType.MASK"/> </summary>
        /// <remarks> Data: None </remarks>
        ENDMASKS = 0x3800,

        /// <summary> Specifies number of pages in library directory </summary>
        /// <remarks> Data: <see cref="short"/> </remarks>
        LIBDIRSIZE = 0x3902,

        /// <summary> Sticks Rules FIle name </summary>
        /// <remarks> Data: <see cref="string"/> </remarks>
        SRFNAME = 0x3A06,

        /// <summary> Array of ACL data </summary>
        /// <remarks> Data: <see cref="short"/> </remarks>
        LIBSECUR = 0x3B02
    }
}
