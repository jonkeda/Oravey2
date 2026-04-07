using Oravey2.MapGen.App.ViewModels;

namespace Oravey2.MapGen.App.Views;

public partial class FigureGeneratorView : ContentPage
{
    public FigureGeneratorView()
    {
        InitializeComponent();
        BindingContext = App.Current?.Handler?.MauiContext?.Services.GetService<FigureGeneratorViewModel>();
    }
}
