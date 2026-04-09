using Oravey2.MapGen.App.ViewModels;
using Oravey2.MapGen.App.Views;

namespace Oravey2.MapGen.App.Pages;

public partial class MainPage : TabbedPage
{
    public MainPage(PipelineWizardViewModel wizardViewModel, IServiceProvider services)
    {
        InitializeComponent();

        var pipelineView = new PipelineWizardView(wizardViewModel, services) { Title = "Pipeline v3" };
        Children.Insert(0, pipelineView);
    }
}
