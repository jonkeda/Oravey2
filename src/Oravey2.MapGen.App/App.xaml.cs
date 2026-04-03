namespace Oravey2.MapGen.App;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new Pages.MainPage())
        {
            Title = "Oravey2 Map Generator",
            Width = 900,
            Height = 700
        };
    }
}
