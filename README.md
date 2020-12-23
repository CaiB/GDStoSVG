# GDS to SVG Converter

Fully integrated system to convert GDSII format files, such as the ones exported from Cadence Virtuoso, into SVG graphics for printing or display.

Prerequisites:
- [.NET Core 3.1](https://dotnet.microsoft.com/download/dotnet-core)

Should work on Windows, Linux, and Mac. Theoretically big-endian platforms are also supported (untested).

## Usage
1) Export your layout as GDSII format from the CAD software you're using. (see below)
2) Run this utility in info mode to determine layers, by running `GDStoSVG.exe <myfile.gds> -info`
3) It will output design units and layer info to the console. Using this layer list, and the generated SVG file (same name as the input GDS), locate components on a specific layer (all items on a layer are grouped together), and determine what each layer ID corresponds to.
4) Create a CSV file with your layer IDs, names, colours, and opacities in the required format (see `GDStoSVG.exe -help` for info).
5) Test your CSV file by running `GDStoSVG.exe <myfile.gds> -csv <layers.csv> -info`.
6) Repeat until you are happy with the layer assignments, and there are no unassigned layers remaining (unless you don't care about those layers).
7) The CSV can be reused for any further exports using the same PDK. In the future, simply run `GDStoSVG.exe <myfile.gds> -csv <layer.csv> [-svg output.svg]`

## Exporting from Cadence Virtuoso 6.1.6
1) From the main Virtuoso window, go to File -> Export -> Stream.
2) Type in a file name, and select your library and top-level cell.
3) Make sure "Translate entire hierarchy" is checked on the "General" tab.
4) Other default configuration should be OK.
5) Use the exported GDS file as directed above.

## Notes
- This is still unfinished. Expect bugs.
- Polygons and paths are output as-is. This means a ton of individual, potentially overlapping polygons. You may want to run a Union in Inkscape or similar software to resolve this.
- Text is not fully working yet.
- Arrays are not yet implemented.