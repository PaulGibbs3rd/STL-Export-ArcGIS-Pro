using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;

namespace STL_Export_Tool
{
    /// <summary>
    /// ViewModel for the "Add Base to STL" dockpane. Lets users pick an existing STL
    /// (an open/unbased mesh) and add a watertight flat base to it using the same
    /// basifier logic as the main export workflow, saving the result as
    /// "{originalName}_with_base.stl".
    /// </summary>
    internal class Dockpane2ViewModel : DockPane, INotifyPropertyChanged
    {
        #region Constants and Fields

        /// <summary>
        /// Unique identifier for this dockpane - must match the ID in Config.daml
        /// </summary>
        private const string _dockPaneID = "STL_Export_Tool_Dockpane2";

        private string _heading = "Add Base to STL";
        private string _status = "Ready";
        private string _inputFilePath = string.Empty;

        // Arbitrary input STL files carry no known real-world scale (STL is unitless),
        // so unlike the main export dockpane's feet-based BaseThickness, this value is
        // applied directly in the STL's own native units (whatever the model was authored in).
        private double _baseThickness = 5.0; // native STL units, added below the mesh's lowest point

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion

        #region Commands

        /// <summary>
        /// Command to browse for the input STL file to add a base to
        /// </summary>
        public ICommand BrowseInputFileCommand => new RelayCommand(BrowseInputFile);

        /// <summary>
        /// Command to run the basifier against the selected input STL
        /// </summary>
        public ICommand AddBaseCommand => new RelayCommand(async () => await AddBaseAsync());

        #endregion

        #region Public Properties

        public string Heading { get => _heading; set { _heading = value; OnPropertyChanged(); } }

        public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }

        /// <summary>
        /// Full path to the input STL file that needs a base added
        /// </summary>
        public string InputFilePath { get => _inputFilePath; set { _inputFilePath = value; OnPropertyChanged(); } }

        /// <summary>
        /// Thickness of the base to add, in the STL's own native units, below the mesh's lowest point
        /// </summary>
        public double BaseThickness { get => _baseThickness; set { _baseThickness = value; OnPropertyChanged(); } }

        #endregion

        #region Show/Activate

        /// <summary>
        /// Shows the dockpane, or activates it if it's already visible
        /// </summary>
        internal static void Show()
        {
            var pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null) return;
            pane.Activate();
        }

        #endregion

        #region Command Implementations

        /// <summary>
        /// Opens a file browser dialog to select the input STL file
        /// </summary>
        private void BrowseInputFile()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select STL file to add a base to",
                Filter = "STL files (*.stl)|*.stl|All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() == true)
            {
                InputFilePath = dlg.FileName;
                Status = "Ready";
            }
        }

        /// <summary>
        /// Reads the selected STL, adds a watertight flat base using the same basifier
        /// logic as the main export workflow, and saves the result alongside the
        /// original file with a "_with_base" suffix.
        /// </summary>
        private async Task AddBaseAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(InputFilePath) || !File.Exists(InputFilePath))
                {
                    MessageBox.Show("Select a valid input STL file.");
                    return;
                }

                if (BaseThickness <= 0)
                {
                    MessageBox.Show("Base thickness must be greater than zero.");
                    return;
                }

                // Applied directly in the STL's own native units - we have no reliable way
                // to know the real-world scale of an arbitrary input file.
                float thicknessInNativeUnits = (float)BaseThickness;

                string directory = Path.GetDirectoryName(InputFilePath);
                string fileNameNoExt = Path.GetFileNameWithoutExtension(InputFilePath);
                string outputPath = Path.Combine(directory ?? string.Empty, $"{fileNameNoExt}_with_base.stl");

                Status = "Adding base…";

                string reason = string.Empty;
                bool ok = await Task.Run(() =>
                    STL_Basifier.TryAddExtrudedBase(InputFilePath, outputPath, thicknessInNativeUnits, padding: 0f, out reason)
                );

                if (!ok)
                {
                    if (reason.Contains("far smaller than the requested base", StringComparison.OrdinalIgnoreCase))
                    {
                        Status = "Failed: mesh footprint too small relative to base thickness.";
                        MessageBox.Show(reason, "Add Base to STL", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        return;
                    }

                    // Fallback method: simple rectangular base
                    MessageBox.Show($"Extruded base failed: {reason}\nTrying fallback method...",
                        "Add Base to STL", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);

                    await Task.Run(() =>
                        STL_Basifier.AddRectangularBaseAuto(
                            InputFilePath, outputPath,
                            thicknessInNativeUnits, outset: 0f, raise: 0.001f)
                    );
                    reason = "Used rectangular base as fallback";
                }

                string methodUsed = string.IsNullOrEmpty(reason) ? "extruded base" : $"fallback method ({reason})";
                Status = $"Base added ({methodUsed}): {outputPath}";
                MessageBox.Show(Status, "Add Base to STL");
            }
            catch (Exception ex)
            {
                Status = $"Failed to add base: {ex.Message}";
                MessageBox.Show(Status, "Add Base to STL", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
