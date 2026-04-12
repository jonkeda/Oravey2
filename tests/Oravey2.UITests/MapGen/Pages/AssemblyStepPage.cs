using Brinell.Maui.Controls.Buttons;
using Brinell.Maui.Controls.Display;
using Brinell.Maui.Controls.Text;
using Brinell.Maui.Interfaces;
using Brinell.Maui.Pages;

namespace Oravey2.UITests.MapGen.Pages;

public class AssemblyStepPage : PageObjectBase<AssemblyStepPage>
{
    public AssemblyStepPage(IMauiTestContext context) : base(context) { }

    public override string Name => "AssemblyStep";

    public override bool IsLoaded(int? timeoutMs = null)
        => ExportToDb.IsExists();

    // Buttons
    public Button<AssemblyStepPage> GenerateScenario
        => Button("AssemblyGenerateScenarioButton");
    public Button<AssemblyStepPage> RebuildCatalog
        => Button("AssemblyCatalogButton");
    public Button<AssemblyStepPage> UpdateManifest
        => Button("AssemblyManifestButton");
    public Button<AssemblyStepPage> Validate
        => Button("AssemblyValidateButton");
    public Button<AssemblyStepPage> ExportToDb
        => Button("AssemblyExportToDbButton");
    public Button<AssemblyStepPage> Complete
        => Button("AssemblyCompleteButton");

    // Labels
    public Label<AssemblyStepPage> StatusText
        => Label("AssemblyStatusText");
    public Label<AssemblyStepPage> ValidationSummary
        => Label("AssemblyValidationSummary");

    // Entries
    public Entry<AssemblyStepPage> ScenarioId
        => Entry("AssemblyScenarioId");
    public Entry<AssemblyStepPage> ScenarioName
        => Entry("AssemblyScenarioName");
    public Entry<AssemblyStepPage> ScenarioDescription
        => Entry("AssemblyScenarioDescription");
}
