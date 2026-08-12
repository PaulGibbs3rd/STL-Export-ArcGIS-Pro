using System;
using System.Threading.Tasks;
using System.Windows;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Mapping;

namespace STL_Export_Tool
{
    /// <summary>
    /// Click–drag a rectangle on the map/scene; writes the axis-aligned envelope to the
    /// dockpane's MinX/MinY/MaxX/MaxY fields.
    /// </summary>
    internal class DrawExtentTool : MapTool
    {
        public DrawExtentTool()
        {
            IsSketchTool = true;
            SketchType = SketchGeometryType.Rectangle;   // drag a rectangle
            SketchOutputMode = SketchOutputMode.Map;     // return geometry in map SR
            UseSnapping = true;
        }

        protected override Task<bool> OnSketchCompleteAsync(Geometry geometry)
        {
            try
            {
                if (geometry == null || geometry.IsEmpty)
                    return Task.FromResult(false);

                var env = geometry.Extent;
                if (env == null || env.IsEmpty)
                    return Task.FromResult(false);

                // Update the dockpane's fields on the UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var pane = FrameworkApplication.DockPaneManager.Find("STL_Export_Tool_Dockpane1") as Dockpane1ViewModel;
                    if (pane != null)
                    {
                        pane.MinX = env.XMin;
                        pane.MinY = env.YMin;
                        pane.MaxX = env.XMax;
                        pane.MaxY = env.YMax;
                        pane.Status = "Extent set from map sketch.";
                    }
                });

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"Failed to accept extent.\n{ex.Message}");
                return Task.FromResult(false);
            }
        }
    }
}
