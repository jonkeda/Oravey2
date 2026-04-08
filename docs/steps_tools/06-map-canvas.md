# Step 06 — Map Canvas Rendering

**Work streams:** WS-Canvas (MAUI GraphicsView rendering)
**Depends on:** Step 04 (ViewModel with parsed data), Step 05 (XAML page)
**User-testable result:** Parse Noord-Holland data → map canvas shows terrain elevation as a coloured heightmap, towns as labelled dots, roads as lines, water bodies as blue polygons. Pan and zoom with mouse.

---

## Goals

1. Create a custom `IDrawable` that renders the world template map with multiple layers.
2. Add pan/zoom via mouse drag and scroll wheel.
3. Bidirectional selection: click a feature on the map → highlight in feature list; select in list → highlight on map.
4. Visual distinction between included (normal), excluded (dimmed/red), and selected (white ring) features.

---

## Problem

After parsing, the user needs a spatial overview of the data to understand what towns, roads, and water bodies exist and where they are before deciding what to cull. A flat list is insufficient for spatial understanding.

---

## Tasks

### 6.1 — WorldTemplateMapDrawable

File: `src/Oravey2.MapGen.App/Views/WorldTemplateMapDrawable.cs`

- [ ] Implement `IDrawable`
- [ ] Properties:
  - `float[,]? ElevationGrid` — heightmap data
  - `IReadOnlyList<TownItem>? Towns`
  - `IReadOnlyList<RoadItem>? Roads`
  - `IReadOnlyList<WaterItem>? WaterBodies`
  - `PointF Offset` — pan offset
  - `float Zoom` — zoom level (default 1.0)
  - `GeoMapper? Mapper` — lat/lon → canvas coordinate conversion

### 6.2 — Coordinate Mapping

- [ ] Create a simple geo-to-canvas mapper:
  - Input: lat/lon bounding box (from preset)
  - Output: canvas pixel coordinates
  - Account for zoom and pan offset
  - Origin = top-left of canvas, Y-down
  - Equirectangular projection (sufficient for Noord-Holland scale)
- [ ] `PointF GeoToCanvas(double lat, double lon)` — converts lat/lon to canvas XY
- [ ] `(double lat, double lon) CanvasToGeo(PointF point)` — inverse for click-to-select

### 6.3 — Layer Rendering

Rendering order (back to front):

**Layer 1 — Elevation background:**
- [ ] Render `ElevationGrid` as a coloured image
- [ ] Colour ramp: deep blue (sea level / negative) → green (low) → brown (mid) → white (high)
- [ ] Cache as `IImage` to avoid per-frame recalculation
- [ ] Only regenerate when data changes or zoom changes significantly

**Layer 2 — Water bodies:**
- [ ] Render as filled blue polygons (semi-transparent)
- [ ] Included: `#4488CCAA`, Excluded: `#44444466`

**Layer 3 — Roads:**
- [ ] Render as polylines
- [ ] Colour by class: motorway=red, trunk=orange, primary=yellow, secondary=white, other=grey
- [ ] Included: full opacity, Excluded: 30% opacity
- [ ] Line width: motorway=3, trunk=2.5, primary=2, secondary=1.5, other=1

**Layer 4 — Towns:**
- [ ] Render as filled circles (radius by category: City=8, Town=6, Village=4, Hamlet=3)
- [ ] Included: white fill, Excluded: grey fill with red outline
- [ ] Selected: additional white ring (radius + 3)

**Layer 5 — Labels:**
- [ ] Town names, positioned above the dot
- [ ] Only show labels at sufficient zoom level (avoid clutter)
- [ ] Font size: City=12, Town=10, Village=8
- [ ] Included: white text, Excluded: grey text

### 6.4 — Pan and Zoom

- [ ] Wire `GraphicsView` gesture recognizers:
  - `PanGestureRecognizer` → update `Offset`, invalidate canvas
  - `PointerGestureRecognizer` → scroll wheel changes `Zoom` (0.5 – 10.0 range)
- [ ] Zoom centred on mouse position
- [ ] Clamp pan so the map doesn't leave the viewport entirely

### 6.5 — Click-to-Select

- [ ] On `GraphicsView.EndInteraction` or pointer click:
  1. Convert canvas point to lat/lon via `CanvasToGeo`
  2. Find nearest feature within a tolerance radius
  3. Priority: town (within 10px) > road (within 5px) > water (point-in-polygon)
  4. Set `IsSelected` on matched item, clear previous selection
  5. Notify ViewModel → scroll feature list to selected item

### 6.6 — List-to-Map Selection

- [ ] When `TownItem.IsSelected` changes in the list, scroll/pan the map to centre on that town
- [ ] Flash or pulse the selection ring briefly

### 6.7 — Add to WorldTemplateView XAML

File: `src/Oravey2.MapGen.App/Views/WorldTemplateView.xaml`

- [ ] Add `GraphicsView` in the map preview area:
  ```xml
  <GraphicsView x:Name="MapCanvas"
                Drawable="{Binding MapDrawable}"
                HeightRequest="400" />
  ```
- [ ] Bind drawable properties to ViewModel data
- [ ] Add layer visibility checkboxes: ☑ Towns ☑ Roads ☑ Water ☐ Railways ☐ Land Use

### 6.8 — Invalidation Strategy

- [ ] ViewModel raises event → code-behind calls `MapCanvas.Invalidate()`
- [ ] Throttle invalidation during pan (max 60 fps)
- [ ] Full redraw on zoom change, data change, or include/exclude toggle
- [ ] Only labels and selection ring on selection change (if possible to partial redraw)

---

## Verify

```bash
dotnet build src/Oravey2.MapGen.App
```

**User test:** Launch app → select Noord-Holland preset → set paths to existing SRTM/OSM data → Parse Data. The map canvas shows:
- Coloured elevation background (flat areas green/blue, no real hills in Noord-Holland)
- Blue water polygons (IJ, Markermeer, canals)
- Red/yellow/white road lines (A-roads prominent)
- White dots for towns with labels

Pan by dragging. Zoom with scroll wheel. Click a town → it gets a white ring and the feature list scrolls to it.
