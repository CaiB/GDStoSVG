﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GDStoSVG
{
    public class GDSReader
    {
        /// <summary> Whether the platform we are running on is little-endian (true in most cases). </summary>
        private readonly bool IsLE;

        /// <summary> A list of the structures present in the file. </summary>
        private readonly List<Structure> Structures = new List<Structure>();

        public GDSReader() { this.IsLE = BitConverter.IsLittleEndian; }

        public void ReadFile(string fileName)
        {
            using (BinaryReader Reader = new BinaryReader(File.Open(fileName, FileMode.Open)))
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
            Console.WriteLine("Read " + this.Structures.Count + " structures.");
        }

        private Structure? CurrentStructure = null;
        private Element? CurrentElement = null;
        private short? CurrentProperty = null;

        private bool ReadRecord(RecordType type, byte[]? data)
        {
            //Console.WriteLine("Reading " + type.ToString());
            Type ElementType;
            switch (type) // List sorted somewhat in order of expected file structure.
            {
                case RecordType.HEADER:
                    if (data == null || data.Length < 2) { throw new InvalidDataException("Header had insufficient data"); }
                    Console.WriteLine(string.Format("File version is 0x{0:X2}{1:X2}", data[0], data[1]));
                    break;
                case RecordType.BGNLIB:
                    // TODO: Read last modified/accessed times
                    break;
                case RecordType.LIBDIRSIZE: // optional
                case RecordType.SRFNAME: // optional
                case RecordType.LIBSECUR: // optional
                    break;
                case RecordType.LIBNAME:
                    // TODO: Read name
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
                    //TODO: Read last modify/access time
                    break;
                case RecordType.STRNAME:
                    if (this.CurrentStructure == null) { throw new InvalidDataException("Structure name found outside of a structure."); }
                    if (data == null || data.Length == 0) { throw new InvalidDataException("Structure name had no data"); }
                    this.CurrentStructure.Name = ParseString(data, 0, data.Length);
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
                    // TODO: Read flags
                    if (data == null || data.Length < 2) { throw new InvalidDataException("Element flags had insufficient data"); }
                    Console.WriteLine(string.Format("Element flags are 0x{0:X2}{1:X2}", data[0], data[1]));
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
                    Tuple<int, int>[] Coords = new Tuple<int, int>[data.Length / 8];
                    for(int i = 0; i < Coords.Length; i++)
                    {
                        Coords[i] = new Tuple<int, int>(ParseInt(data, i * 8), ParseInt(data, (i * 8) + 4));
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
                    if (this.CurrentElement.GetType() != typeof(Path)) { throw new InvalidOperationException("Cannot assign path type to non-path element."); }
                    if (data == null || data.Length < 2) { throw new InvalidDataException("Path type assignment had insufficient data"); }
                    ((Path)this.CurrentElement).PathType = ParseShort(data, 0);
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
                case RecordType.ENDEXTN:
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
                        SRef.XReflect = Reflect;
                        SRef.MagnificationAbsolute = AbsMag;
                        SRef.AngleAbsolute = AbsAng;
                    }
                    else if (this.CurrentElement is ArrayRef ARef)
                    {
                        ARef.XReflect = Reflect;
                        ARef.MagnificationAbsolute = AbsMag;
                        ARef.AngleAbsolute = AbsAng;
                    }
                    else if (this.CurrentElement is Text Txt)
                    {
                        Txt.XReflect = Reflect;
                        Txt.MagnificationAbsolute = AbsMag;
                        Txt.AngleAbsolute = AbsAng;
                    }
                    else { throw new InvalidOperationException("Tried to assign structure transform properties to non-reference element."); }
                    break;
                case RecordType.MAG:
                    if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign magnification with no element."); }
                    if (data == null || data.Length < 8) { throw new InvalidDataException("Element magnification had insufficient data"); }
                    double Mag = ParseDouble(data, 0);
                    if (this.CurrentElement is StructureRef StRef) { StRef.Magnification = Mag; }
                    else if (this.CurrentElement is ArrayRef ArRef) { ArRef.Magnification = Mag; }
                    else if (this.CurrentElement is Text Txt2) { Txt2.Magnification = Mag; }
                    else { throw new InvalidOperationException("Tried to assign magnification to element that cannot take magnification parameter."); }
                    break;
                case RecordType.ANGLE:
                    if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign angle with no element."); }
                    if (data == null || data.Length < 8) { throw new InvalidDataException("Element angle had insufficient data"); }
                    double Angle = ParseDouble(data, 0);
                    if (this.CurrentElement is StructureRef StRef2) { StRef2.Angle = Angle; }
                    else if (this.CurrentElement is ArrayRef ArRef2) { ArRef2.Angle = Angle; }
                    else if (this.CurrentElement is Text Txt3) { Txt3.Angle = Angle; }
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
                case RecordType.PROPATTR:
                    if (this.CurrentProperty != null) { throw new InvalidDataException("New property starting before previous one had value assigned."); }
                    if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign property with no element to attach to."); }
                    if (data == null || data.Length < 2) { throw new InvalidDataException("Property key had insufficient data"); }
                    this.CurrentProperty = ParseShort(data, 0);
                    break;
                case RecordType.PROPVALUE:
                    if (this.CurrentProperty != null) { throw new InvalidDataException("New property starting before previous one had value assigned."); }
                    if (this.CurrentElement == null) { throw new InvalidDataException("Trying to assign property with no element to attach to."); }
                    if (this.CurrentProperty == null) { throw new InvalidDataException("Trying to assign property data without key."); }
                    if (data == null || data.Length == 0) { throw new InvalidDataException("Property value had insufficient data"); }
                    if (this.CurrentElement.Properties == null) { this.CurrentElement.Properties = new Dictionary<short, string>(); }
                    string Value = ParseString(data, 0, data.Length);
                    this.CurrentElement.Properties.Add((short)this.CurrentProperty, Value);
                    this.CurrentProperty = null;
                    break;
                case RecordType.ENDEL:
                    if (this.CurrentStructure == null) { throw new InvalidDataException("Element ended outside of structure."); }
                    if (this.CurrentElement == null) { throw new InvalidDataException("Element ending before starting."); }
                    if (this.CurrentStructure.Elements == null) { this.CurrentStructure.Elements = new List<Element>(); }
                    if (!this.CurrentElement.Check()) { Console.WriteLine("Element does not have all required data present."); }
                    this.CurrentStructure.Elements.Add(this.CurrentElement);
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

        private short ParseShort(byte[] data, int position)
        {
            if (this.IsLE) { return (short)((data[position] << 8) | data[position + 1]); }
            else { return (short)(data[position] | (data[position + 1] << 8)); }
        }

        private int ParseInt(byte[] data, int position)
        {
            if (this.IsLE) { return ((data[position] << 24) | (data[position + 1] << 16) | (data[position + 2] << 8) | data[position + 3]); }
            else { return (data[position] | (data[position + 1] << 8) | (data[position + 2] << 16) | (data[position + 3] << 24)); }
        }

        private double ParseDouble(byte[] data, int position)
        {
            byte[] DoubleData = new byte[8];
            Array.Copy(data, position, DoubleData, 0, 8);
            if (this.IsLE) { Array.Reverse(DoubleData); }
            return BitConverter.ToDouble(DoubleData);
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
}
