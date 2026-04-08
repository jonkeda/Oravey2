using Oravey2.MapGen.WorldTemplate;

namespace Oravey2.MapGen.ViewModels.WorldTemplate;

public class TownItem : ViewModelBase
{
    public TownEntry Entry { get; }

    private bool _isIncluded = true;
    public bool IsIncluded { get => _isIncluded; set => SetProperty(ref _isIncluded, value); }

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    public string Name => Entry.Name;
    public TownCategory Category => Entry.Category;
    public int Population => Entry.Population;
    public double Lat => Entry.Latitude;
    public double Lon => Entry.Longitude;

    public TownItem(TownEntry entry) => Entry = entry;
}
