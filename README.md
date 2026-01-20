# STL Export Tool for ArcGIS Pro

An ArcGIS Pro add-in that exports 3D scene content to STL format with automatic base generation for 3D printing.

## Features

- **Export 3D Scenes to STL**: Export terrain, buildings, and other 3D features from ArcGIS Pro Local Scenes to STL format
- **Automatic Base Generation**: Adds a solid, printable base to exported models using advanced mesh processing
- **Interactive Extent Selection**: Draw export extents directly on the map or use the current view
- **3D Print Ready**: Generates watertight, solid models optimized for 3D printing

## Requirements

- **ArcGIS Pro 3.5** or later
- **.NET 8.0** Runtime
- **Local Scene** (the tool requires a Local Scene viewing mode)

## Installation

### Option 1: Install from Release

1. Download the latest `.esriAddinX` file from the [Releases](../../releases) page
2. Double-click the `.esriAddinX` file to install
3. Restart ArcGIS Pro if it's currently running

### Option 2: Build from Source

1. Clone this repository:
   ```bash
   git clone https://github.com/yourusername/STL-Export-Tool.git
   ```

2. Open the solution in Visual Studio 2022 or later

3. Restore NuGet packages:
   ```bash
   dotnet restore
   ```

4. Build the solution:
   ```bash
   dotnet build --configuration Release
   ```

5. The `.esriAddinX` file will be in the `bin\Release` folder

6. Double-click the `.esriAddinX` file to install in ArcGIS Pro

## Usage

### Opening the Tool

1. Open ArcGIS Pro and create or open a **Local Scene** project
2. Navigate to the **STL Export** tab in the ribbon
3. Click **STL Export Dockpane** to open the tool

### Exporting a Model

1. **Set Export Extent**:
   - **Option A**: Click "Draw Extent" and draw a rectangle on your scene
   - **Option B**: Click "Set from View" to use the current view extent
   - **Option C**: Manually enter coordinates in the Min/Max X/Y fields

2. **Configure Output**:
   - Choose an output folder
   - Enter a filename (`.stl` extension will be added automatically)

3. **Base Generation Options** (Optional):
   - **Add Base**: Check to add a solid base for 3D printing
   - **Base Thickness**: Set the thickness in scene units (typically meters)

4. **Export**:
   - Click **Export** to generate the STL file
   - The tool will automatically add a base if enabled
   - A backup of the original export is saved as `.bak`

## How It Works

### Export Process

1. **Scene Export**: Uses ArcGIS Pro's built-in STL exporter to convert 3D scene content
2. **Base Generation**: Applies one of two methods to add a printable base:
   - **Primary Method**: Extruded base - creates vertical walls from mesh boundary edges
   - **Fallback Method**: Rectangular base - simple box under the mesh
3. **File Management**: Creates backups and safely replaces the original with the enhanced version

### Base Generation Algorithms

The tool uses a sophisticated approach to create 3D-printable bases:

**Extruded Base Method** (Primary):
- Detects vertices at the mesh boundary edges
- Projects these vertices straight down to create base level
- Connects edge vertices to their projections with vertical walls
- Adds a flat bottom surface
- Creates a watertight, solid object

**Rectangular Base Method** (Fallback):
- Creates a simple rectangular box under the entire mesh
- Ensures a flat, stable base for printing
- Used when boundary detection fails

## Project Structure

```
STL Export Tool/
??? Config.daml                 # Add-in configuration and UI definitions
??? Module1.cs                  # Add-in module entry point
??? Dockpane1.xaml             # UI layout (XAML)
??? Dockpane1ViewModel.cs      # Main ViewModel and export logic
??? displayDockpane.cs         # Button command to show dockpane
??? DrawExtentTool.cs          # Interactive extent drawing tool
??? STL_Basifier.cs            # STL base generation algorithms
??? Images/                     # Icons and images
```

## Key Components

### STL_Basifier.cs
Core STL processing engine that:
- Reads binary and ASCII STL files
- Implements mesh analysis algorithms
- Generates printable bases using geometry processing

### Dockpane1ViewModel.cs
Main application logic:
- Manages UI state and data binding
- Coordinates the export workflow
- Integrates ArcGIS Pro API with STL processing

### DrawExtentTool.cs
Custom map tool for interactive extent selection

## Technical Details

- **Language**: C# 12.0
- **Framework**: .NET 8.0
- **Platform**: ArcGIS Pro SDK
- **UI Framework**: WPF (Windows Presentation Foundation)

## Troubleshooting

### "Export requires a Local Scene" error
**Solution**: Ensure you're working in a Local Scene, not a Global Scene or 2D Map.

### Base generation fails
**Solution**: The tool will automatically try the fallback method. Check that:
- The mesh has valid geometry
- The base thickness is appropriate for your scene units

### Export produces empty file
**Solution**: Verify that:
- There's 3D content within your export extent
- The extent coordinates are valid
- You have write permissions to the output folder

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built with the [ArcGIS Pro SDK](https://pro.arcgis.com/en/pro-app/latest/sdk/)
- Inspired by the need for better 3D GIS to 3D printing workflows

## Support

For issues, questions, or suggestions:
- Open an [Issue](../../issues)
- Check existing [Discussions](../../discussions)

## Author

**Paul** ([@pau11750](https://github.com/pau11750))

## Version History

### Version 1.0.0 (Current)
- Initial release
- STL export with base generation
- Interactive extent selection
- Automatic base generation with fallback methods

---

**Made with ?? for the GIS and 3D Printing communities**
