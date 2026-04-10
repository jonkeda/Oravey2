using Oravey2.MapGen.ViewModels;

namespace Oravey2.MapGen.App.ViewModels;

public abstract class AppBaseViewModel : BaseViewModel
{
    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }
}
