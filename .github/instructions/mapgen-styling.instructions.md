---
applyTo: src/Oravey2.MapGen.App/**
---

# MAUI Styling Guidelines for Oravey2.MapGen Tools

All XAML views in `Oravey2.MapGen.App` use a unified style system defined in two resource dictionaries merged via `App.xaml`. Follow these rules when creating or editing views.

## Architecture

```
App.xaml → MergedDictionaries
  ├─ Themes/LightPalette.xaml   (color definitions only)
  └─ Styles/Controls.xaml       (Style definitions referencing palette keys)
```

## Rules

### 1. Never hardcode colors
Use `{StaticResource <key>}` for every color value. All palette keys are defined in the active palette file (`Themes/LightPalette.xaml`).

**Common keys:** `Primary`, `OnPrimary`, `Secondary`, `Background`, `Surface`, `SurfaceVariant`, `OnSurface`, `OnSurfaceVariant`, `Outline`, `Error`, `Success`, `ActionBlue`, `Danger`.

### 2. Use named styles for buttons
| Intent | Style |
|--------|-------|
| Main action | `Style="{StaticResource PrimaryButton}"` |
| Secondary / neutral | `Style="{StaticResource SecondaryButton}"` |
| Destructive / cancel | `Style="{StaticResource DangerButton}"` |
| Success / confirm | `Style="{StaticResource SuccessButton}"` |
| Text-only / ghost | `Style="{StaticResource GhostButton}"` |

### 3. Use named styles for text
| Intent | Style |
|--------|-------|
| Page header (24pt bold) | `Style="{StaticResource HeaderLabel}"` |
| Section header (16pt bold) | `Style="{StaticResource SectionLabel}"` |
| Small muted text | `Style="{StaticResource CaptionLabel}"` |
| Monospace log output | `Style="{StaticResource MonospaceLabel}"` |

Plain `<Label>` without a style inherits the implicit style (14pt, OnSurface color, OpenSans-Regular).

### 4. Use named styles for containers
| Intent | Style |
|--------|-------|
| Card panel | `Style="{StaticResource CardBorder}"` + `<Border.StrokeShape><RoundRectangle CornerRadius="8" /></Border.StrokeShape>` |
| Elevated card | `Style="{StaticResource ElevatedCard}"` + StrokeShape |
| Log output area | `Style="{StaticResource LogFrame}"` + StrokeShape |

Each `Border` must still declare its own `<Border.StrokeShape>` inline (MAUI limitation with shared shape instances).

### 5. Override only what differs
Apply a named style first, then override individual properties:
```xml
<Button Style="{StaticResource PrimaryButton}" Padding="8,4" />
```

### 6. Implicit styles you get for free
These targets are styled automatically — do **not** re-declare their base properties:
- `ContentPage` → BackgroundColor
- `Label` → TextColor, FontFamily, FontSize
- `Entry` → TextColor, BackgroundColor, PlaceholderColor
- `Editor` → TextColor, BackgroundColor, PlaceholderColor
- `Picker` → TextColor, BackgroundColor
- `ProgressBar` → ProgressColor
- `ActivityIndicator` → Color

**Exception — DataTemplates and re-scoped BindingContext:** On WinUI, implicit styles do **not** propagate into `CollectionView.ItemTemplate`, `DataTemplate`, or containers with an explicit `BindingContext` reassignment. Every `Label` inside these scopes **must** have an explicit `TextColor`:
```xml
<!-- Inside a DataTemplate — ALWAYS set TextColor explicitly -->
<Label Text="{Binding Name}" TextColor="{StaticResource OnSurface}" />
<Label Text="{Binding Detail}" TextColor="{StaticResource OnSurfaceVariant}" />
```
Similarly, `<Span>` elements inside `FormattedString` never inherit implicit styles and always need explicit `TextColor`.

### 7. Use Border instead of Frame
`Frame` is deprecated in MAUI. Use `Border` with the `CardBorder` or `LogFrame` style for card/log areas.

### 8. Adding a new palette color
Add the `<Color>` to **both** `LightPalette.xaml` (and `DarkPalette.xaml` when it exists) with the same `x:Key`.

### 9. Adding a new control style
Add the style to `Styles/Controls.xaml`. Reference only palette keys — never hardcode hex colors in the styles file.
