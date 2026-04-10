# Unified Styling Proposal for Oravey2 Tools

## Problem

All MAUI views in MapGen.App use **inline hardcoded styles** — colors, font sizes, padding, and corner radii are repeated across every XAML file. There are no shared `Style` definitions, no `ControlTemplate` resources, and no theme dictionaries beyond the 8 color keys in `App.xaml`.

This causes:

- **Repetition** — the same button styling (`BackgroundColor="#2D2D30"`, `Padding="12,6"`, `CornerRadius="8"`) appears dozens of times.
- **Inconsistency** — subtle drift between views (e.g. `Padding="8,4"` vs `"12,6"` vs `"16,8"` on buttons).
- **Expensive theme changes** — switching to a light theme or adjusting the palette requires editing every XAML file.

## Goals

| # | Goal |
|---|------|
| 1 | Define a **single resource dictionary** with named styles for all common controls. |
| 2 | Views reference styles by name (`Style="{StaticResource CardBorder}"`) or override individual properties when needed. |
| 3 | Support **light and dark palettes** by swapping only the color dictionary, not the styles. |
| 4 | Keep it simple — no dynamic theme switching at runtime for now; palette is compile-time or app-start. |

## Architecture

```
App.xaml
└─ MergedDictionaries
   ├─ Themes/LightPalette.xaml   ← color & brush definitions
   ├─ Themes/DarkPalette.xaml    ← (future)
   └─ Styles/Controls.xaml       ← Style definitions referencing palette keys
```

### Layer 1 — Palette (colors only)

Each palette file defines the same set of keyed `Color` and optional `SolidColorBrush` resources. The active palette is merged first so all subsequent styles can reference these keys.

| Key | Purpose | Light value | Dark value (current) |
|-----|---------|-------------|----------------------|
| `Primary` | Primary accent | `#6750A4` | `#512BD4` |
| `OnPrimary` | Text on primary | `#FFFFFF` | `#FFFFFF` |
| `Secondary` | Secondary accent | `#625B71` | `#DFD8F7` |
| `Tertiary` | Tertiary accent | `#7D5260` | `#2B0B98` |
| `Background` | Page background | `#F5F5F5` | `#1E1E2E` |
| `Surface` | Card / panel fill | `#FFFFFF` | `#2D2D3D` |
| `SurfaceVariant` | Elevated surface | `#E7E0EC` | `#383850` |
| `OnSurface` | Default text | `#1C1B1F` | `#CDD6F4` |
| `OnSurfaceVariant` | Secondary text | `#49454F` | `#AAAAAA` |
| `Outline` | Borders | `#CAC4D0` | `#555555` |
| `Error` | Error states | `#B3261E` | `#F38BA8` |
| `Success` | Success states | `#2E7D32` | `#A6E3A1` |
| `ActionBlue` | Async / progress | `#1565C0` | `#0078D4` |
| `Danger` | Destructive actions | `#C62828` | `#6E3630` |

### Layer 2 — Control Styles

`Styles/Controls.xaml` contains implicit and explicit `Style` definitions that reference palette keys via `{StaticResource …}`. Views consume them in three ways:

#### a) Implicit styles (apply automatically)

```xml
<!-- Every Label gets theme text color automatically -->
<Style TargetType="Label">
    <Setter Property="TextColor" Value="{StaticResource OnSurface}" />
    <Setter Property="FontFamily" Value="OpenSans-Regular" />
    <Setter Property="FontSize" Value="14" />
</Style>
```

#### b) Named styles (opt-in)

```xml
<Style x:Key="HeaderLabel" TargetType="Label">
    <Setter Property="FontSize" Value="24" />
    <Setter Property="FontAttributes" Value="Bold" />
    <Setter Property="TextColor" Value="{StaticResource OnSurface}" />
</Style>

<Style x:Key="SectionLabel" TargetType="Label">
    <Setter Property="FontSize" Value="16" />
    <Setter Property="FontAttributes" Value="Bold" />
    <Setter Property="TextColor" Value="{StaticResource OnSurface}" />
</Style>
```

Usage:

```xml
<Label Text="Region Template" Style="{StaticResource HeaderLabel}" />
```

#### c) Direct override

When a view needs a one-off deviation it can set properties directly; the rest still comes from the style:

```xml
<Button Style="{StaticResource PrimaryButton}" Text="Custom" BackgroundColor="Orange" />
```

## Proposed Named Styles

### Buttons

| Style Key | Purpose |
|-----------|---------|
| `PrimaryButton` | Main action (accent color) |
| `SecondaryButton` | Secondary action (surface color, border) |
| `DangerButton` | Destructive action (red/danger color) |
| `SuccessButton` | Positive confirmation (green/success color) |
| `GhostButton` | Text-only button (transparent background) |

### Text

| Style Key | Purpose |
|-----------|---------|
| `HeaderLabel` | Page / section header (24 bold) |
| `SectionLabel` | Sub-section header (16 bold) |
| `CaptionLabel` | Small secondary text (12, muted) |
| `MonospaceLabel` | Log / code output (Consolas 12) |

### Containers

| Style Key | Purpose |
|-----------|---------|
| `CardBorder` | Rounded card with surface fill and outline border |
| `ElevatedCard` | Card with SurfaceVariant fill (slight elevation) |
| `LogFrame` | Fixed-height frame for console-style output |

### Inputs

| Style Key | Purpose |
|-----------|---------|
| `StandardEntry` | Themed text entry with outline border |
| `StandardEditor` | Multi-line editor with consistent sizing |
| `StandardPicker` | Themed dropdown picker |

### Indicators

| Style Key | Purpose |
|-----------|---------|
| `StandardProgressBar` | Themed progress bar (ActionBlue) |
| `StandardActivityIndicator` | Themed spinner (ActionBlue) |

## Migration Strategy

1. **Create palette + controls files** — no views change yet, app still works.
2. **Merge dictionaries in App.xaml** — implicit styles take effect immediately.
3. **Migrate view-by-view** — replace inline colors/sizes with `Style="{StaticResource …}"` or remove properties that now inherit from implicit styles. Each view is a single commit.
4. **Delete orphan hardcoded values** — final pass to ensure no remaining magic strings.

## Folder Structure

```
src/Oravey2.MapGen.App/
├─ Themes/
│  └─ LightPalette.xaml
├─ Styles/
│  └─ Controls.xaml
├─ App.xaml               ← merges Themes + Styles
├─ Views/
│  └─ …                   ← reference styles by key
```

## Out of Scope (for now)

- Runtime theme switching (light ↔ dark toggle).
- Shared style library NuGet for non-MAUI tools (RegionTemplateTool is console-only).
- Animations or visual state managers.
