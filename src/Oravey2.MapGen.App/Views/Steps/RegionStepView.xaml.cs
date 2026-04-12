using Oravey2.MapGen.ViewModels;

namespace Oravey2.MapGen.App.Views;

public partial class RegionStepView : ContentView
{
    private readonly RegionStepViewModel _viewModel;
    private readonly IServiceProvider _services;

    public RegionStepView(RegionStepViewModel viewModel, IServiceProvider services)
    {
        _viewModel = viewModel;
        _services = services;
        BindingContext = viewModel;
        InitializeComponent();

        _viewModel.CreateContentPackRequested += OnCreateContentPackRequested;
    }

    private async void OnCreateContentPackRequested()
    {
        if (Application.Current?.Windows.FirstOrDefault()?.Page is not Page page) return;

        // Step 1: Choose genre
        var existingGenres = _viewModel.GetExistingGenres();
        var choices = new List<string>(existingGenres) { "New Genre…" };
        var genre = await page.DisplayActionSheetAsync(
            "Choose Genre", "Cancel", null, choices.ToArray());

        if (string.IsNullOrEmpty(genre) || genre == "Cancel") return;

        if (genre == "New Genre…")
        {
            genre = await page.DisplayPromptAsync(
                "New Genre",
                "Enter genre name (e.g. Apocalyptic, Fantasy):",
                placeholder: "Apocalyptic");
            if (string.IsNullOrWhiteSpace(genre)) return;
        }

        // Step 2: Pick region via OSM picker
        var pickerVM = _services.GetRequiredService<RegionPickerViewModel>();
        var dialog = new RegionPickerDialog(pickerVM);

        var tcs = new TaskCompletionSource<RegionPickerViewModel?>();

        pickerVM.RegionSelected += _ =>
        {
            tcs.TrySetResult(pickerVM);
        };

        dialog.Disappearing += (_, _) =>
        {
            tcs.TrySetResult(null);
        };

        await page.Navigation.PushModalAsync(new NavigationPage(dialog));
        var result = await tcs.Task;
        if (result?.SelectedRegion is null) return;

        // Step 3: Confirm
        var region = result.SelectedRegion;
        var regionCode = region.Iso3166_2?.FirstOrDefault()
                      ?? region.Iso3166Alpha2?.FirstOrDefault()
                      ?? "unknown";

        var confirmed = await page.DisplayAlertAsync(
            "Create Content Pack",
            $"Create content pack chain for {genre} / {region.Name} ({regionCode})?",
            "Create", "Cancel");

        if (!confirmed) return;

        _viewModel.EnsurePackChain(genre, region);
    }
}
