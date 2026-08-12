using ArcGIS.Desktop.Framework.Contracts;

namespace STL_Export_Tool
{
    internal class displayAddBaseDockpane : Button
    {
        protected override void OnClick()
        {
            Dockpane2ViewModel.Show();
        }
    }
}
