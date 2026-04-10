# Step 07b — Town Design UI

## Overview

MAUI `ContentView` for the Town Design step (pipeline step 5).
The user designs each curated town via LLM — reviewing landmark, key locations,
layout style, and hazards — then accepts or regenerates before moving on.

## Layout

Two-column master–detail layout using a horizontal `Grid`.

```
┌────────────────────────────────────────────────────────────────────────────┐
│  ⑤ Town Design                                                             │
│  Use LLM to generate a design for each curated town.                       │
│                                                                            │
│  ┌────────────────────────┐  ┌──────────────────────────────────────────┐  │
│  │ Town List              │  │ Design Detail (selected town)            │  │
│  │                        │  │                                          │  │
│  │  ✅ Havenburg          │  │  Town: Havenburg                        │  │
│  │  ✅ Dunewijk           │  │  Role: trade-hub · Faction: Harbour ... │  │
│  │  — Sluispoort     ◄───┤  │  Threat: 4                              │  │
│  │  — Veendam             │  │                                          │  │
│  │                        │  │  ┌────────────────────────────────────┐  │  │
│  │                        │  │  │ 🏛 Landmark                       │  │  │
│  │                        │  │  │ Fort Kijkduin (large)             │  │  │
│  │  3/5 designed          │  │  │ A massive coastal fortress ...    │  │  │
│  └────────────────────────┘  │  └────────────────────────────────────┘  │  │
│                              │                                          │  │
│  ┌────────────────────────┐  │  ┌────────────────────────────────────┐  │  │
│  │ [Design Town]          │  │  │ 📍 Key Locations (5)              │  │  │
│  │ [Design All Remaining] │  │  │                                    │  │  │
│  │ [Cancel]  ◌ spinner    │  │  │  The Drydock Market · shop · med  │  │  │
│  └────────────────────────┘  │  │  Old Lighthouse · lookout · small │  │  │
│                              │  │  ...                               │  │  │
│                              │  └────────────────────────────────────┘  │  │
│                              │                                          │  │
│                              │  Layout: compound                        │  │
│                              │                                          │  │
│                              │  ┌────────────────────────────────────┐  │  │
│                              │  │ ⚠ Hazards (1)                     │  │  │
│                              │  │                                    │  │  │
│                              │  │  flooding — south-west waterfront  │  │  │
│                              │  │  The harbour district floods ...   │  │  │
│                              │  └────────────────────────────────────┘  │  │
│                              │                                          │  │
│                              │  [Accept]  [Re-generate]                 │  │
│                              └──────────────────────────────────────────┘  │
│                                                                            │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │ LLM Log (collapsible)                                                │  │
│  │  → 14:32:01  Design Havenburg: You are an RPG town designer ...      │  │
│  │  ← 14:32:04  {"townName":"Havenburg","landmark":{...}}               │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                                                            │
│  [Next →]                                                                  │
└────────────────────────────────────────────────────────────────────────────┘
```

## Sections

### 1. Header

| Element     | Binding / Value |
|-------------|-----------------|
| Title label | `"⑤ Town Design"`, FontSize 24, Bold |
| Subtitle    | `"Use LLM to generate a design for each curated town."`, TextColor `#AAAAAA` |

### 2. Master–Detail Grid

```
ColumnDefinitions="280,16,*"
```

#### 2a. Left Column — Town List

A `CollectionView` bound to `{Binding Towns}` with single-selection bound to
`{Binding SelectedTown}`.

Each item template (`DataTemplate x:DataType="vm:TownDesignItem"`):

| Element | Binding |
|---------|---------|
| Status icon | `{Binding StatusIcon}` — "✅" or "—" |
| Game name | `{Binding GameName}`, Bold, FontSize 14 |
| Real name | `{Binding RealName, StringFormat='({0})'}`, TextColor `#888888` |

Below the list:
- Progress label: `{Binding ProgressText}` → e.g. `"3/5 designed"`

#### 2b. Left Column — Action Buttons

Below the town list, inside a `VerticalStackLayout`:

| Button | Command | Condition |
|--------|---------|-----------|
| **Design Town** | `{Binding DesignTownCommand}` | SelectedTown ≠ null, not running |
| **Design All Remaining** | `{Binding DesignAllCommand}` | Any undesigned, not running |
| **Cancel** | `{Binding CancelCommand}` | IsVisible when `IsRunning` |

`ActivityIndicator` bound to `{Binding IsRunning}`.

Status label: `{Binding StatusText}`, TextColor `#888888`.

#### 2c. Right Column — Design Detail Panel

Visible only when `{Binding HasSelection}` is true.
Content bound through `{Binding SelectedTown}` as the implicit data context
(via `BindingContext="{Binding SelectedTown}"`).

##### Town Summary Card

`Border` with rounded corners, dark background `#252530`.

| Element | Binding |
|---------|---------|
| Game name | `{Binding GameName}`, FontSize 18, Bold |
| Role + Faction | `{Binding Role}` · `{Binding Faction}` |
| Threat level | `{Binding ThreatLevel, StringFormat='Threat: {0}'}` |
| Description | `{Binding Description}`, TextColor `#CCCCCC`, FontSize 12 |

##### Landmark Card

`Border` with header "🏛 Landmark". Visible when `Design` is not null.

| Element | Binding |
|---------|---------|
| Name | `{Binding LandmarkName}`, Bold |
| Size | `{Binding Design.Landmark.SizeCategory}`, badge style |
| Visual description | `{Binding Design.Landmark.VisualDescription}`, italic, TextColor `#CCCCCC` |

##### Key Locations Card

`Border` with header "📍 Key Locations ({KeyLocationCount})".
Contains a `CollectionView` bound to `{Binding Design.KeyLocations}`.

Each item (`DataTemplate x:DataType="gen:KeyLocation"`):

| Element | Binding |
|---------|---------|
| Name | `{Binding Name}`, Bold, FontSize 13 |
| Purpose | `{Binding Purpose}`, TextColor `#AAAAAA` |
| Size | `{Binding SizeCategory}`, badge |
| Visual description | `{Binding VisualDescription}`, FontSize 11, TextColor `#CCCCCC` |

##### Layout Style Badge

Single `Label` bound to `{Binding LayoutStyle}` inside a small `Border`
with rounded corners and accent background `#2D5A27`.

##### Hazards Card

`Border` with header "⚠ Hazards ({HazardCount})". Visible when `HazardCount > 0`.
Contains a `CollectionView` bound to `{Binding Design.Hazards}`.

Each item (`DataTemplate x:DataType="gen:EnvironmentalHazard"`):

| Element | Binding |
|---------|---------|
| Type | `{Binding Type}`, Bold, TextColor `#E5A00D` |
| Location hint | `{Binding LocationHint}`, TextColor `#AAAAAA` |
| Description | `{Binding Description}`, FontSize 11, TextColor `#CCCCCC` |

##### Detail Action Buttons

| Button | Command | Condition |
|--------|---------|-----------|
| **Accept** | `{Binding AcceptCommand}` (on parent VM) | HasPendingDesign, not running |
| **Re-generate** | `{Binding RegenerateCommand}` (on parent VM) | SelectedTown ≠ null, not running |

### 3. LLM Log

Collapsible section at the bottom. `CollectionView` bound to `{Binding LlmLog}`
with `MaximumHeightRequest="250"`.

Item template (`DataTemplate x:DataType="vm:LlmLogEntry"`):

| Element | Binding |
|---------|---------|
| Direction | `{Binding Direction}`, Bold, TextColor `#0078D4` |
| Timestamp | `{Binding Timestamp, StringFormat='{0:HH:mm:ss}'}`, TextColor `#666666` |
| Content | `{Binding Content}`, FontFamily Consolas, MaxLines 20, word-wrap |

### 4. Next Button

| Button | Command | Condition |
|--------|---------|-----------|
| **Next →** | `{Binding NextCommand}` | AllDesigned and not running |

## Color Palette

Follows existing step views:

| Token | Hex | Usage |
|-------|-----|-------|
| Panel background | `#1E1E1E` | Card borders, detail sections |
| Section background | `#252530` | Summary card, grouped sections |
| Border stroke | `#444444` | All card borders |
| Primary action | `#0078D4` | Next button, activity indicator |
| Success action | `#238636` | Design Town button |
| Neutral action | `#2D2D30` | Design All, secondary buttons |
| Danger action | `#6E3630` | Cancel button |
| Layout badge | `#2D5A27` | Layout style pill |
| Hazard accent | `#E5A00D` | Hazard type labels |
| Muted text | `#888888` | Subtitles, progress |
| Body text | `#CCCCCC` | Descriptions |
| Bright text | `White` | Names, headings |

## Xaml Namespaces

```xml
xmlns:vm="clr-namespace:Oravey2.MapGen.ViewModels;assembly=Oravey2.MapGen"
xmlns:gen="clr-namespace:Oravey2.MapGen.Generation;assembly=Oravey2.MapGen"
```

`x:DataType="vm:TownDesignStepViewModel"` on the root `ContentView`.

## Code-Behind (`TownDesignStepView.xaml.cs`)

Minimal — constructor takes `TownDesignStepViewModel`, sets `BindingContext`.
No event handlers needed; all interactions go through commands.

```csharp
public partial class TownDesignStepView : ContentView
{
    public TownDesignStepView(TownDesignStepViewModel viewModel)
    {
        BindingContext = viewModel;
        InitializeComponent();
    }
}
```

## Wiring

In `PipelineWizardView.xaml.cs`, update factory registration for step 5:

```csharp
_viewModel.RegisterStepViewFactory(5, () =>
    new TownDesignStepView(_viewModel.TownDesignStepVM));
```

## ViewModel Surface

All bindings already exist on `TownDesignStepViewModel` and `TownDesignItem`:

| Property / Command | Type | Notes |
|--------------------|------|-------|
| `Towns` | `ObservableCollection<TownDesignItem>` | Master list |
| `SelectedTown` | `TownDesignItem?` | Selected via CollectionView |
| `HasSelection` | `bool` | Computed |
| `IsRunning` | `bool` | Spinner + button guards |
| `StatusText` | `string` | Status bar |
| `DesignedCount` | `int` | Progress numerator |
| `TotalCount` | `int` | Progress denominator |
| `ProgressText` | `string` | "3/5 designed" |
| `AllDesigned` | `bool` | Next gate |
| `LlmLog` | `ObservableCollection<LlmLogEntry>` | Log entries |
| `DesignTownCommand` | `ICommand` | Design selected |
| `DesignAllCommand` | `ICommand` | Batch design |
| `AcceptCommand` | `ICommand` | Save pending |
| `RegenerateCommand` | `ICommand` | Re-run LLM |
| `CancelCommand` | `ICommand` | Abort |
| `NextCommand` | `ICommand` | Proceed |

`TownDesignItem` properties:

| Property | Type | Notes |
|----------|------|-------|
| `GameName` | `string` | Display name |
| `RealName` | `string` | OSM name |
| `Role` | `string` | Town role |
| `Faction` | `string` | Town faction |
| `ThreatLevel` | `int` | 1–10 |
| `Description` | `string` | Narrative |
| `StatusIcon` | `string` | "✅" / "—" |
| `IsDesigned` | `bool` | Has saved design |
| `HasPendingDesign` | `bool` | Unsaved result |
| `Design` | `TownDesign?` | Full design data |
| `LandmarkName` | `string?` | Computed from Design |
| `KeyLocationCount` | `int` | Computed |
| `LayoutStyle` | `string?` | Computed |
| `HazardCount` | `int` | Computed |
