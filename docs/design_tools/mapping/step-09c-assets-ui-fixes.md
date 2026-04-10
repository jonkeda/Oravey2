# Step 09c — Assets Step UI Fixes

## Problem

Step 7 (3D Assets) has two display issues visible in the current build:

### Issue 1: Left-panel asset list appears empty

The `CollectionView` bound to `FilteredAssets` renders items but all text is
invisible. The DataTemplate for `AssetItem` has Labels without explicit
`TextColor`, and inside a `CollectionView.ItemTemplate` on WinUI, implicit
styles from `Application.Resources` do not propagate. This is the same root
cause identified in [RCA-005](../../.my/rca/rca-005-townmaps-buildings-zones-blank.md):

```xml
<!-- CURRENT — TextColor falls back to platform default (white on WinUI) -->
<Label Grid.Column="1" Text="{Binding LocationName}" FontSize="14"
       FontAttributes="Bold"  />
<Label Grid.Column="1" Grid.Row="1" Text="{Binding PromptSnippet}"
       FontSize="11" ... LineBreakMode="TailTruncation" />
```

### Issue 2: Right-panel detail header card blank

The right panel's header card binds to `SelectedAsset` with
`x:DataType="vm:AssetItem"`. The Labels for `LocationName`, `TownName`,
`SizeCategory`, and `StatusIcon` also lack explicit `TextColor`:

```xml
<Label Text="{Binding LocationName}" FontSize="18"
       FontAttributes="Bold"  />
```

## Root Cause

Same as RCA-005. On WinUI, `CollectionView.ItemTemplate` and manual
`BindingContext` re-scopes do not propagate implicit `Style TargetType="Label"`
from `Application.Resources`. Labels without explicit `TextColor` inherit the
platform-default foreground, which on WinUI with a light-theme background
resolves to white — rendering text invisible against the white `CardBorder`
background.

## Fix

Add explicit `TextColor="{StaticResource OnSurface}"` to all Labels inside:

1. **Left panel** — `CollectionView.ItemTemplate` (DataTemplate for AssetItem)
2. **Right panel** — header card (`LocationName`)
3. **Right panel** — town/size/status labels in the sub-header

Labels that already have explicit `TextColor` (e.g., `OnSurfaceVariant` for
secondary text) are unaffected.

### File: `src/Oravey2.MapGen.App/Views/Steps/AssetsStepView.xaml`

#### Left panel DataTemplate

```xml
<!-- BEFORE -->
<Label Grid.Column="1" Text="{Binding LocationName}" FontSize="14"
       FontAttributes="Bold"  />

<!-- AFTER -->
<Label Grid.Column="1" Text="{Binding LocationName}" FontSize="14"
       FontAttributes="Bold" TextColor="{StaticResource OnSurface}" />
```

#### Right panel header

```xml
<!-- BEFORE -->
<Label Text="{Binding LocationName}" FontSize="18"
       FontAttributes="Bold"  />

<!-- AFTER -->
<Label Text="{Binding LocationName}" FontSize="18"
       FontAttributes="Bold" TextColor="{StaticResource OnSurface}" />
```

## Broader Pattern

Every `Label` inside a `CollectionView.ItemTemplate` or a container with
explicit `BindingContext` reassignment needs an explicit `TextColor` on WinUI.
This should be verified across all step views. The styling instruction file
(`.github/instructions/mapgen-styling.instructions.md`) should be updated to
document this requirement.

## Related

- [RCA-005: Buildings and Zones blank in Town Maps](../../.my/rca/rca-005-townmaps-buildings-zones-blank.md)
- [Step 09: Meshy 3D Asset Generation](step-09-meshy-assets.md)
- [Step 09b: Dummy Mesh Assigner](step-09b-dummy-mesh-assigner.md)
