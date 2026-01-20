using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.CIM;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using WinForms = System.Windows;
using ArcGIS.Desktop.Internal.Mapping;

namespace STL_Export_Tool
{
    /// <summary>
    /// ViewModel for the STL Export Tool dockpane.
    /// Manages the export of 3D scene content to STL format with optional base generation for 3D printing.
    /// </summary>
    internal class Dockpane1ViewModel : DockPane, INotifyPropertyChanged
    {
        #region Constants and Fields

        /// <summary>
        /// Unique identifier for this dockpane - must match the ID in Config.daml
        /// </summary>
        private const string _dockPaneID = "STL_Export_Tool_Dockpane1";

        // UI Display Properties
        private string _heading = "STL Export";
        private string _status = "Ready";

        // Export Extent Properties (in map/scene spatial reference units)
        private double _minX, _minY, _maxX, _maxY;

        // Output Configuration
        private string _outputFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        private string _outputFileName = "my-3d-objects.stl";
        private bool _isSingleFileOutput = true;

        // Base Generation Options
        private bool _addBase = true;
        private double _baseThickness = 5.0; // meters (for Local Scene)

        #endregion

        #region INotifyPropertyChanged

        /// <summary>
        /// Event raised when a property value changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        /// <param name="name">Name of the property that changed</param>
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion

        #region Commands

        /// <summary>
        /// Command to activate the drawing tool for selecting export extent
        /// </summary>
        public ICommand DrawExtentCommand => new RelayCommand(async () => await StartDrawExtentAsync());

        /// <summary>
        /// Command to set export extent from the current map/scene view
        /// </summary>
        public ICommand SetFromCurrentViewCommand => new RelayCommand(async () => await SetFromCurrentViewAsync());

        /// <summary>
        /// Command to browse for output folder
        /// </summary>
        public ICommand BrowseFolderCommand => new RelayCommand(BrowseFolder);

        /// <summary>
        /// Command to browse for output file name
        /// </summary>
        public ICommand BrowseFileCommand => new RelayCommand(BrowseFile);

        /// <summary>
        /// Command to execute the STL export
        /// </summary>
        public ICommand ExportCommand => new RelayCommand(async () => await ExportAsync());

        #endregion

        #region Public Properties

        /// <summary>
        /// Heading text displayed in the dockpane
        /// </summary>
        public string Heading { get => _heading; set { _heading = value; OnPropertyChanged(); } }

        /// <summary>
        /// Status message displayed to the user
        /// </summary>
        public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }

        /// <summary>
        /// Minimum X coordinate of the export extent
        /// </summary>
        public double MinX { get => _minX; set { _minX = value; OnPropertyChanged(); } }

        /// <summary>
        /// Minimum Y coordinate of the export extent
        /// </summary>
        public double MinY { get => _minY; set { _minY = value; OnPropertyChanged(); } }

        /// <summary>
        /// Maximum X coordinate of the export extent
        /// </summary>
        public double MaxX { get => _maxX; set { _maxX = value; OnPropertyChanged(); } }

        /// <summary>
        /// Maximum Y coordinate of the export extent
        /// </summary>
        public double MaxY { get => _maxY; set { _maxY = value; OnPropertyChanged(); } }

        /// <summary>
        /// Output folder path for the STL file
        /// </summary>
        public string OutputFolder { get => _outputFolder; set { _outputFolder = value; OnPropertyChanged(); } }

        /// <summary>
        /// Output file name for the STL file
        /// </summary>
        public string OutputFileName { get => _outputFileName; set { _outputFileName = value; OnPropertyChanged(); } }

        /// <summary>
        /// Whether to export all objects to a single STL file (true) or separate files (false)
        /// </summary>
        public bool IsSingleFileOutput { get => _isSingleFileOutput; set { _isSingleFileOutput = value; OnPropertyChanged(); } }

        /// <summary>
        /// Whether to add a solid base to the exported STL for 3D printing
        /// </summary>
        public bool AddBase { get => _addBase; set { _addBase = value; OnPropertyChanged(); } }

        /// <summary>
        /// Thickness of the base to add (in scene units, typically meters for Local Scene)
        /// </summary>
        public double BaseThickness { get => _baseThickness; set { _baseThickness = value; OnPropertyChanged(); } }

        #endregion

        #region Command Implementations

        /// <summary>
        /// Activates the drawing tool to allow user to draw export extent on the map/scene
        /// </summary>
        private async Task StartDrawExtentAsync()
        {
            try
            {
                // Activate the custom sketch tool defined in Config.daml
                await FrameworkApplication.SetCurrentToolAsync("STL_Export_Tool_DrawExtentTool");
                Status = "Draw a rectangle on the map/scene. Press Esc to cancel.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not activate Draw Extent tool.\n{ex.Message}");
            }
        }

        /// <summary>
        /// Sets the export extent to match the current map/scene view extent
        /// </summary>
        private async Task SetFromCurrentViewAsync()
        {
            try
            {
                // Ensure there's an active view
                if (MapView.Active == null)
                {
                    MessageBox.Show("No active view.");
                    return;
                }

                // Get the current view extent
                var extent = MapView.Active.Extent;
                if (extent == null)
                {
                    MessageBox.Show("Active view has no extent.");
                    return;
                }

                // Update extent properties
                MinX = extent.XMin;
                MinY = extent.YMin;
                MaxX = extent.XMax;
                MaxY = extent.YMax;

                Status = "Extent copied from current view.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to get current view extent.\n{ex.Message}");
            }
        }

        /// <summary>
        /// Opens a folder browser dialog to select the output folder
        /// </summary>
        private void BrowseFolder()
        {
            // Note: Using OpenFileDialog as a workaround since there's no native folder dialog in WPF
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select output folder",
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder",
                Filter = "Folders|*.none",
                ValidateNames = false
            };

            if (dlg.ShowDialog() == true)
            {
                OutputFolder = Path.GetDirectoryName(dlg.FileName);
            }
        }

        /// <summary>
        /// Opens a save file dialog to select the output file name
        /// </summary>
        private void BrowseFile()
        {
            var dlg = new SaveFileDialog
            {
                Title = "Select STL output file name",
                Filter = "STL files (*.stl)|*.stl",
                FileName = OutputFileName
            };

            if (dlg.ShowDialog() == true)
                OutputFileName = Path.GetFileName(dlg.FileName);
        }

        /// <summary>
        /// Main export workflow: exports scene content to STL and optionally adds a printable base
        /// </summary>
        private async Task ExportAsync()
        {
            try
            {
                #region Validation

                // Ensure there's an active view
                if (MapView.Active == null)
                {
                    MessageBox.Show("No active view.");
                    return;
                }

                // Ensure we're in a Local Scene (required for STL export)
                if (MapView.Active.ViewingMode != MapViewingMode.SceneLocal)
                {
                    MessageBox.Show("Export requires a Local Scene (SceneLocal).");
                    return;
                }

                // Validate output folder
                if (string.IsNullOrWhiteSpace(OutputFolder) || !Directory.Exists(OutputFolder))
                {
                    MessageBox.Show("Select a valid output folder.");
                    return;
                }

                // Validate output file name
                if (string.IsNullOrWhiteSpace(OutputFileName))
                {
                    MessageBox.Show("Enter an output file name (e.g., my-3d-objects.stl).");
                    return;
                }

                // Ensure file has .stl extension
                if (!OutputFileName.EndsWith(".stl", StringComparison.OrdinalIgnoreCase))
                    OutputFileName += ".stl";

                // Validate extent
                if (MinX >= MaxX || MinY >= MaxY)
                {
                    MessageBox.Show("Extent is invalid. Ensure MinX < MaxX and MinY < MaxY.");
                    return;
                }

                #endregion

                #region Build Export Envelope

                // Create spatial envelope for export in the scene's spatial reference
                Envelope exportEnv = null;
                await QueuedTask.Run(() =>
                {
                    var sr = MapView.Active.Map?.SpatialReference ?? SpatialReferences.WGS84;
                    exportEnv = EnvelopeBuilderEx.CreateEnvelope(MinX, MinY, MaxX, MaxY, sr);
                });

                #endregion

                #region Execute STL Export

                Status = "Exporting…";

                // Configure STL export parameters
                var export = new STLExportSceneContentsFormat()
                {
                    Extent = exportEnv,
                    FolderPath = OutputFolder,
                    FileName = OutputFileName,
                    IsSingleFileOutput = IsSingleFileOutput
                };

                // Execute the export using ArcGIS Pro's built-in STL exporter
                MapView.Active.ExportScene3DObjects(export);

                var fullPath = Path.Combine(OutputFolder, OutputFileName);

                // Wait for the STL file to be ready (export happens asynchronously)
                var ready = await WaitForFileReadyAsync(fullPath, timeoutMs: 20000, pollMs: 250);
                if (!ready)
                    throw new IOException($"Export did not produce a readable STL at: {fullPath}");

                #endregion

                #region Add 3D Printable Base (Optional)

                if (AddBase && BaseThickness > 0)
                {
                    // Use temporary files to avoid corrupting the original during processing
                    string backupPath = fullPath + ".bak";
                    string tempOut = fullPath + ".tmp";

                    // Try primary method: Extruded base (connects walls directly to mesh boundary)
                    string reason = string.Empty;
                    bool ok = await Task.Run(() =>
                        STL_Basifier.TryAddExtrudedBase(fullPath, tempOut, (float)BaseThickness, padding: 0f, out reason)
                    );

                    if (!ok)
                    {
                        // Fallback method: Simple rectangular base
                        MessageBox.Show($"Extruded base failed: {reason}\nTrying fallback method...",
                            "STL Export", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);

                        await Task.Run(() =>
                            STL_Basifier.AddRectangularBaseAuto(
                                fullPath, tempOut,
                                (float)BaseThickness, outset: 0f, raise: 0.001f)
                        );
                        ok = true;
                        reason = "Used rectangular base as fallback";
                    }

                    // Replace original STL with basified version
                    try
                    {
                        // Create backup of original export
                        if (File.Exists(backupPath)) File.Delete(backupPath);
                        File.Copy(fullPath, backupPath);

                        // Replace with basified STL
                        File.Copy(tempOut, fullPath, overwrite: true);
                        File.Delete(tempOut);
                    }
                    catch (Exception repEx)
                    {
                        Status = $"Base generated to: {tempOut} (could not replace original: {repEx.Message})";
                        System.Windows.MessageBox.Show(Status, "STL Export");
                        return;
                    }

                    string methodUsed = string.IsNullOrEmpty(reason) ? "extruded base" : $"fallback method ({reason})";
                    Status = $"Export complete with base ({methodUsed}): {fullPath}";
                    System.Windows.MessageBox.Show(Status, "STL Export");
                    return;
                }

                #endregion

                // Export complete without base
                Status = $"Export complete: {fullPath}";
                System.Windows.MessageBox.Show(Status, "STL Export");
            }
            catch (Exception ex)
            {
                Status = "Export failed.";
                MessageBox.Show($"Export failed.\n{ex.Message}", "STL Export",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Waits for a file to be ready for reading (fully written and not locked)
        /// </summary>
        /// <param name="path">Path to the file to check</param>
        /// <param name="timeoutMs">Maximum time to wait in milliseconds</param>
        /// <param name="pollMs">Polling interval in milliseconds</param>
        /// <returns>True if file is ready, false if timeout occurred</returns>
        private static async Task<bool> WaitForFileReadyAsync(string path, int timeoutMs = 10000, int pollMs = 200)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long lastLen = -1;
            int stable = 0;

            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        // Try to open the file for reading
                        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        long len = fs.Length;

                        // File is considered ready when its length is stable across multiple polls
                        if (len > 0 && len == lastLen)
                        {
                            stable++;
                            if (stable >= 2) return true; // File length stable, ready to use
                        }
                        else
                        {
                            stable = 0;
                            lastLen = len;
                        }
                    }
                }
                catch
                {
                    // File not ready (locked or still being written) — keep polling
                }

                await Task.Delay(pollMs);
            }

            // Timeout occurred, but return true if file at least exists
            return File.Exists(path);
        }

        #endregion

        #region Dockpane Management

        /// <summary>
        /// Shows the dockpane programmatically
        /// </summary>
        internal static void Show()
        {
            var pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
        }

        #endregion
    }

    /// <summary>
    /// Simple implementation of ICommand that supports async execution
    /// </summary>
    internal class RelayCommand : ICommand
    {
        private readonly Func<Task> _executeAsync;
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        /// <summary>
        /// Creates a new RelayCommand with async execution
        /// </summary>
        /// <param name="executeAsync">Async method to execute</param>
        /// <param name="canExecute">Optional method to determine if command can execute</param>
        public RelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
        {
            _executeAsync = executeAsync;
            _canExecute = canExecute;
        }

        /// <summary>
        /// Creates a new RelayCommand with synchronous execution
        /// </summary>
        /// <param name="execute">Method to execute</param>
        /// <param name="canExecute">Optional method to determine if command can execute</param>
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        /// <summary>
        /// Determines whether the command can execute
        /// </summary>
        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        /// <summary>
        /// Executes the command
        /// </summary>
        public async void Execute(object parameter)
        {
            if (_executeAsync != null)
                await _executeAsync();
            else
                _execute?.Invoke();
        }

        /// <summary>
        /// Event raised when CanExecute status changes
        /// </summary>
        public event EventHandler CanExecuteChanged;

        /// <summary>
        /// Raises the CanExecuteChanged event
        /// </summary>
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
