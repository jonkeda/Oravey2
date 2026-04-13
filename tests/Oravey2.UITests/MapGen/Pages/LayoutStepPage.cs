using Brinell.Maui.Controls.Buttons;
using Brinell.Maui.Controls.Display;
using Brinell.Maui.Interfaces;
using Brinell.Maui.Pages;

namespace Oravey2.UITests.MapGen.Pages;

public class LayoutStepPage : PageObjectBase<LayoutStepPage>
{
    public LayoutStepPage(IMauiTestContext context) : base(context) { }

    public override string Name => "LayoutStep";

    public override bool IsLoaded(int? timeoutMs = null)
        => ResetViewButton.IsExists();

    // Labels / Display Elements
    public Label<LayoutStepPage> StatusText
        => Label("LayoutStatusText");
    public Label<LayoutStepPage> GridDimensions
        => Label("LayoutGridDimensions");
    public Label<LayoutStepPage> BuildingCount
        => Label("LayoutBuildingCount");
    public Label<LayoutStepPage> RoadLength
        => Label("LayoutRoadLength");
    public Label<LayoutStepPage> WaterArea
        => Label("LayoutWaterArea");
    public Label<LayoutStepPage> ZoomLevel
        => Label("LayoutZoomLevel");

    // Buttons
    public Button<LayoutStepPage> ResetViewButton
        => Button("LayoutResetViewButton");
    public Button<LayoutStepPage> FitToScreenButton
        => Button("LayoutFitToScreenButton");

    // Custom Elements (may not have direct Page Object support)
    // Automation IDs for custom elements:
    // - LayoutUseSpatialSpecToggle (Switch)
    // - LayoutVisualization (SpatialSpecVisualizationControl)
}
