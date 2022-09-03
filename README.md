# GDS to SVG Converter

Fully integrated system to convert GDSII format files, such as the ones exported from Cadence Virtuoso, into SVG graphics for printing or display.

Prerequisites:
- [.NET 6](https://dotnet.microsoft.com/download/dotnet)
- [Clipper 2](https://github.com/AngusJohnson/Clipper2) (included)

Should work on Windows, Linux, and Mac. Theoretically big-endian platforms are also supported (untested).

## Usage
1) Export your layout as GDSII format from the CAD software you're using. (see below)
2) Run this utility in info mode to determine layers, by running `GDStoSVG.exe <myfile.gds> -info`
3) Using the layer list the tool outputs and the generated SVG file (same name as the input GDS), determine what each layer ID corresponds to in your PDK's stackup
    - If your SVG file is too large to open, you may want to export only a sub-unit to start, by using the `-unit <UNIT>` parameter, with one of the units listed from step 2
4) Create a CSV file with your layer IDs, names, colours, and opacities in the required format:
    - `<Name>,<ID in range -32768 to 32767>,<Colour, RRGGBB hex>,<Opacity in range 0.0 to 1.0>`
    - Example: `M1,50,3333CC,0.8`
5) Test your CSV file by running `GDStoSVG.exe <myfile.gds> -csv <layers.csv> -info`
6) Repeat until you are happy with the layer assignments, and there are no unassigned layers remaining (unless you don't care about those layers)
7) The CSV can be reused for any further exports using the same PDK. In the future, simply run `GDStoSVG.exe <myfile.gds> -csv <layer.csv> [-svg output.svg]`
8) You can optionally export with `-optimize` to attempt to simplify the resulting geometry into as few shapes as possible. This will make the process take much longer, but is still several orders of magnitude faster than a union operation in Inkscape

There are some additional options available as well. Check `GDStoSVG.exe -help` for explanations.

## Exporting from Cadence Virtuoso 6.1.6
1) From the main Virtuoso window, go to File -> Export -> Stream
2) Type in a file name, and select your library and top-level cell
3) Make sure "Translate entire hierarchy" is checked on the "General" tab
4) Other default configuration should be OK
5) Use the exported GDS file as directed above

## Notes
- This is still unfinished. Expect bugs.
- Text is not fully working yet.
- Arrays are not yet implemented.

## Performance Tests
The below tests were conducted on v0.2.0.13, on a 5800X3D system with 32GB of 3600MHz DDR4:
| Design | GDS Size | Mode | Time Taken | SVG Size |
|---|---|---|---|---|
| 13x16b Custom-designed register file (hierarchical) | 174 KB | (default) | 180ms | 5.75 MB |
| 13x16b Custom-designed register file (hierarchical) | 174 KB | `-ignoretext` | 150ms | 5.05 MB |
| 13x16b Custom-designed register file (hierarchical) | 174 KB | `-optimize` | 520ms | 3.51 MB |
| 13x16b Custom-designed register file (hierarchical) | 174 KB | `-ignoretext` `-optimize` | 510ms | 2.81 MB |
| Medium-size SAPRed DSP system (mostly flat) | 151 MB | (default) | 65s | 3.50 GB |
| Medium-size SAPRed DSP system (mostly flat) | 151 MB | `-ignoretext`| 43s | 2.23 GB |
| Medium-size SAPRed DSP system (mostly flat) | 151 MB | `-optimize` | 40m 21s | 2.75 GB |
| Medium-size SAPRed DSP system (mostly flat) | 151 MB | `-ignoretext` `-optimize` | 38m 55s | 1.48 GB |

There are still significant optimizations that could be done, I'd estimate runtime could be reduced by an order of magnitude. Maybe one day :)