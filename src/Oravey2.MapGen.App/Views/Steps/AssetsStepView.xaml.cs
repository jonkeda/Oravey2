using Oravey2.MapGen.ViewModels;

namespace Oravey2.MapGen.App.Views;

public partial class AssetsStepView : ContentView
{
    public AssetsStepView(AssetsStepViewModel viewModel)
    {
        // Register value converter for progress bar (int 0-100 → double 0-1)
        Resources.Add("IntToProgressConverter", new IntToProgressConverter());
        InitializeComponent();
        BindingContext = viewModel;
    }
}

internal sealed class IntToProgressConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter,
        System.Globalization.CultureInfo culture) =>
        value is int i ? i / 100.0 : 0.0;

    public object ConvertBack(object? value, Type targetType, object? parameter,
        System.Globalization.CultureInfo culture) =>
        value is double d ? (int)(d * 100) : 0;
}
