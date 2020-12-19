using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GDStoSVG
{
    public class GDSReader
    {
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

            /// <summary> Tape number </summary>
            /// <remarks> Data: <see cref="short"/> </remarks>
            TAPENUM = 0x3202,

            /// <summary> Tape code </summary>
            /// <remarks> Data: <see cref="short"/> </remarks>
            TAPECODE = 0x3302,

            /// <summary> Format type </summary>
            /// <remarks> Data: <see cref="short"/> </remarks>
            FORMAT = 0x3602,

            /// <summary> List of layers </summary>
            /// <remarks> Data: <see cref="string"/> </remarks>
            MASK = 0x3706,

            /// <summary> End of <see cref="RecordType.MASK"/> </summary>
            /// <remarks> Data: None </remarks>
            ENDMASKS = 0x3800
        }

        public void ReadFile(string fileName)
        {
            using (BinaryReader Reader = new BinaryReader(File.Open(fileName, FileMode.Open)))
            {
                long FileLength = Reader.BaseStream.Length;
                while (Reader.BaseStream.Position < FileLength)
                {
                    ushort Length = Reader.ReadUInt16();
                    RecordType Type = (RecordType)Reader.ReadUInt16();
                    byte[] Data = Reader.ReadBytes(Length - 4); // Remove 4 bytes for the header for data length
                    ReadRecord(Type, Data);
                }
            }
        }

        private void ReadRecord(RecordType type, byte[] data)
        {
            switch(type)
            {
                case RecordType.HEADER:
                    Console.WriteLine(string.Format("File version is 0x{0:X2}{1:X2}", data[0], data[1]));
                    break;
                //case RecordType.
            }
        }

        private class Structure
        {
            private string a;
        }
    }
}
