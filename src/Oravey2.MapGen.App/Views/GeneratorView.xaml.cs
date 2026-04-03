using Oravey2.MapGen.App.ViewModels;

namespace Oravey2.MapGen.App.Views;

public partial class GeneratorView : ContentPage
{
    public GeneratorView()
    {
        InitializeComponent();
        BindingContext = App.Current?.Handler?.MauiContext?.Services.GetService<GeneratorViewModel>();
    }
}
