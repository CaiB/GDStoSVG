using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GDStoSVG
{
    public class SVGWriter
    {
        private readonly StreamWriter Writer;

        public SVGWriter(string fileName)
        {
            this.Writer = new StreamWriter(fileName);
            this.Writer.WriteLine(@"<?xml version=""1.0"" standalone=""no""?>");
            this.Writer.WriteLine(@"<!DOCTYPE svg PUBLIC "" -//W3C//DTD SVG 1.1//EN"" ""http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd"">");
            this.Writer.WriteLine(@"<svg viewBox = ""0 0 200 200"" version = ""1.1"">");

        }

        public void Finish()
        {
            this.Writer.WriteLine(@"</svg>");
            this.Writer.Flush();
            this.Writer.Close();
            this.Writer.Dispose();
        }

    }
}
