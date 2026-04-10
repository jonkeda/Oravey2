using Oravey2.MapGen.ViewModels;

namespace Oravey2.MapGen.App.Views;

public partial class AssemblyStepView : ContentView
{
    public AssemblyStepView(AssemblyStepViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
