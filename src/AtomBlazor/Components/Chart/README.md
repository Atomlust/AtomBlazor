# AtomBlazor Chart Components

A comprehensive charting library for AtomBlazor built on [Apache ECharts](https://echarts.apache.org/), providing a declarative, composable API with full design system integration and automatic theme support.

## Overview

The chart components use a **Recharts-inspired composition pattern** where you build charts by nesting child components:

- **Declarative API** — compose charts from `<XAxis>`, `<YAxis>`, `<ChartTooltip>`, `<Line>`, etc.
- **Reflection-based data binding** — pass any `IEnumerable` and reference properties by name via `DataKey`
- **Theme integration** — CSS custom properties (`--chart-1` through `--chart-5`) resolved at runtime
- **Automatic dark/light mode** — `MutationObserver` watches for theme changes and re-renders
- **Responsive** — `ResizeObserver` on every chart instance

## Architecture

```
Chart/
├── Configuration/
│   ├── ChartColor.cs         # CSS variable color utilities
│   ├── ChartConfig.cs        # Series-to-label/color mapping
│   └── ChartSeriesConfig.cs  # Individual series configuration
├── Core/
│   ├── ChartBase.razor        # Base chart component (JS interop, child registration)
│   ├── ChartBase.razor.cs
│   ├── ChartContainer.razor   # Styled container wrapper (Card-like)
│   ├── ChartContainer.razor.cs
│   ├── DataExtractor.cs       # Reflection-based data extraction with ConcurrentDictionary cache
│   └── IChartComponent.cs     # IChartComponent, IChartSeries, IFillComponent interfaces
├── Composables/
│   ├── XAxis.razor/.razor.cs
│   ├── YAxis.razor/.razor.cs
│   ├── Grid.razor/.razor.cs
│   ├── ChartTooltip.razor/.razor.cs
│   └── ChartLegend.razor/.razor.cs
├── Enums/
│   ├── AxisType.cs            # Category, Value, Time, Log
│   ├── CurveType.cs           # Linear, Smooth, Step, StepBefore, StepAfter
│   ├── GradientDirection.cs   # Vertical, Horizontal
│   ├── LabelPosition.cs       # Top, Bottom, Left, Right, Inside, Outside, Center, etc.
│   ├── LegendPosition.cs      # Top, Bottom, Left, Right, Hidden
│   ├── LineStyleType.cs       # Solid, Dashed, Dotted
│   ├── RadarShape.cs          # Polygon, Circle
│   └── TooltipIndicator.cs    # Dot, Line, Dashed, None
├── Fill/
│   ├── Fill.razor/.razor.cs
│   ├── LinearGradient.razor/.razor.cs
│   └── GradientStop.razor/.razor.cs
├── Models/
│   ├── EChartsOption.cs       # Root option model
│   ├── EChartsAxis.cs         # Axis configuration
│   ├── EChartsSeries.cs       # Series + styles
│   ├── EChartsGrid.cs
│   ├── EChartsTooltip.cs
│   ├── EChartsLegend.cs
│   ├── EChartsGradient.cs
│   └── EChartsRadar.cs
├── Series/
│   ├── SeriesBase.cs          # Abstract base (DataKey, Name, Color, Stacked, Fill)
│   ├── Line.razor/.razor.cs
│   ├── Bar.razor/.razor.cs
│   ├── Area.razor/.razor.cs
│   ├── Pie.razor/.razor.cs
│   ├── Radar.razor/.razor.cs
│   ├── RadialBar.razor/.razor.cs
│   ├── Rose.cs                # derives from Pie, adds Mode
│   ├── Sankey.razor/.razor.cs
│   └── CenterLabel.razor/.razor.cs
└── Types/
    ├── LineChart.cs
    ├── BarChart.cs
    ├── AreaChart.cs
    ├── PieChart.cs
    ├── RadarChart.cs
    ├── RadialBarChart.cs
    ├── RoseChart.cs
    └── SankeyChart.cs
```

### How It Works

Child components register with their parent chart via a `CascadingValue`:

```
ChartBase (cascades itself)
  ├── XAxis       → registers via ParentChart.RegisterComponent(this)
  ├── YAxis       → same
  ├── Grid        → same
  ├── ChartTooltip → same
  ├── ChartLegend → same
  └── Line : SeriesBase → same
       └── Fill (cascades itself)
            └── LinearGradient → registers with Fill
                 └── GradientStop → registers with LinearGradient
```

All child components implement `IChartComponent` with `ApplyTo(EChartsOption)`. On render, `ChartBase.BuildOption()` calls `ApplyTo()` on each registered component, serializes the `EChartsOption` to JSON, and passes it to the JS renderer.

### Data Flow

```
C# Parameters → BuildOption() → EChartsOption model → JsonSerializer → JS update() → echarts.setOption()
```

### JS Layer

- **echarts-renderer.js** — Manages ECharts instance lifecycle (initialize, update, resize, dispose) with `ResizeObserver` per chart and theme change callbacks
- **chart-theme.js** — Recursively resolves CSS `var()` references via `getComputedStyle()`, converts OKLCH colors to hex via canvas 2D context, and watches `<html>` for theme changes via `MutationObserver`

## Available Chart Types

### LineChart

Visualize trends and changes over time.

```razor
<LineChart Data="@data" Height="300px">
    <XAxis DataKey="month" />
    <YAxis />
    <ChartTooltip />
    <Line DataKey="desktop" Name="Desktop" Color="var(--chart-1)" />
    <Line DataKey="mobile" Name="Mobile" Color="var(--chart-2)" Curve="CurveType.Smooth" />
</LineChart>
```

### BarChart

Vertical and horizontal bars for comparing values across categories.

```razor
<BarChart Data="@data" Height="300px">
    <XAxis DataKey="month" />
    <YAxis />
    <ChartTooltip />
    <Bar DataKey="desktop" Name="Desktop" Color="var(--chart-1)" BorderRadius="4" />
    <Bar DataKey="mobile" Name="Mobile" Color="var(--chart-2)" BorderRadius="4" />
</BarChart>
```

Set `Horizontal="true"` on `BarChart` for horizontal bars.

### AreaChart

Filled areas emphasizing volume and magnitude.

```razor
<AreaChart Data="@data" Height="300px">
    <XAxis DataKey="month" />
    <YAxis />
    <ChartTooltip />
    <Area DataKey="desktop" Name="Desktop" Color="var(--chart-1)" Curve="CurveType.Smooth" />
</AreaChart>
```

### PieChart

Circular charts for proportions and part-to-whole relationships.

```razor
<PieChart Data="@data" Height="300px">
    <ChartTooltip />
    <Pie DataKey="visitors" NameKey="browser" />
</PieChart>
```

Set `InnerRadius` on `Pie` for a donut chart. Add `<CenterLabel>` for center text.

### RadarChart

Spider/web charts for comparing multiple variables.

```razor
<RadarChart Data="@data" IndicatorKey="month" Height="300px" Shape="RadarShape.Circle">
    <ChartTooltip />
    <ChartLegend />
    <Radar DataKey="desktop" Name="Desktop" Color="var(--chart-1)" FillOpacity="0.6" />
    <Radar DataKey="mobile" Name="Mobile" Color="var(--chart-2)" FillOpacity="0.6" />
</RadarChart>
```

### RadialBarChart

Circular progress bars and radial visualizations.

```razor
<RadialBarChart Data="@data" Height="300px" StartAngle="90" EndAngle="-270">
    <ChartTooltip />
    <ChartLegend />
    <RadialBar DataKey="visitors" NameKey="browser" RoundCap="true" />
</RadialBarChart>
```

### RoseChart

A pie whose sectors also vary in radius, so categories rank by length rather than by angle.

```razor
<RoseChart Data="@data" Height="300px">
    <ChartTooltip />
    <Rose DataKey="visitors" NameKey="browser" Mode="RoseMode.Radius" />
</RoseChart>
```

`Rose` derives from `Pie`, so every Pie parameter — `InnerRadius`, `OuterRadius`, the labels, a
child `CenterLabel` — applies here too. `Mode` maps to the ECharts `roseType` option:
`RoseMode.Radius` keeps the pie's proportional angles and adds radius on top, `RoseMode.Area` gives
every sector the same angle and varies the radius alone.

### MapChart (v4.1.0)

A world choropleth that colors countries from a numeric dataset. Country boundaries
ship with the library and load only when a map is used; no API key or external map
service is required.

```razor
<MapChart Data="@visitors" Height="420px" OnDataPointClick="SelectCountry">
    <ChartTooltip />
    <Map CountryKey="Country" DataKey="Visitors" Name="Visitors" />
</MapChart>
```

`visitors` can contain records such as `new CountryVisitors("US", 12400)`.
`CountryKey` accepts ISO alpha-2 codes (`US`), alpha-3 codes (`USA`), and English
names from the bundled Natural Earth dataset, ignoring case and outer whitespace.
Use one `Map` series per chart and aggregate to one row per country. Unknown
countries are ignored; the first row wins for duplicate countries, even when
identified by different aliases. Missing, invalid, and non-finite values retain
the no-data fill; zero is a valid measured value.

The default color scale spans zero and the supplied values (including negative
values). Add `<VisualMap Min="0" Max="50000" Colors="@colors" />` for a fixed
range and your own low-to-high color palette. Fixed ranges make multiple maps or
periods comparable. Values outside an explicit range use ECharts' out-of-range
styling. `Map.NoDataColor` defaults to `var(--muted)` and `BorderColor` to
`var(--background)`. `Roam="true"` enables pan and zoom; `ShowLabel="true"` displays
country names. Both are off by default. Fill gradients are configured through
`VisualMap`, rather than the inherited series `Color` parameter.

Tooltips use country names. `OnDataPointClick` reports the canonical English name,
value, and original dataset index. A country absent from the dataset reports
`DataIndex = -1` and a null value. Provide a data table alongside the chart when
users need a keyboard-accessible alternative to pointing at countries.

The bundled map contains 241 countries and regions, excluding Antarctica. Small
islands may be absent or hard to see at world scale. See
[map provenance and rebuilding](../../wwwroot/maps/README.md).

### SankeyChart

Shows how a quantity splits and recombines as it moves between stages.

```razor
<SankeyChart Data="@flows" Height="400px">
    <ChartTooltip />
    <Sankey SourceKey="from" TargetKey="to" DataKey="value" />
</SankeyChart>
```

Bind a collection of **links**, not of nodes: each row gives a source name, a target name and a
value, and the node list is derived from the names in the order they are first seen — which is also
the order they take their colours from the chart palette.

A sankey is a directed acyclic graph. ECharts throws rather than drawing when the links form a
cycle, which leaves the whole chart blank, so a link that would close a cycle — including a link
from a node to itself — is dropped and the rest of the diagram is drawn. A row missing either name,
or carrying a value that is not a number, is dropped for the same reason.

A node in the outermost column points its label at the edge of the canvas, where ECharts draws it
and lets it overflow, so those nodes are given the opposite `LabelPosition` and the text turns
inwards instead.

## Usage

### Basic Example

```razor
<LineChart Data="@data" Height="300px">
    <XAxis DataKey="month" />
    <YAxis />
    <ChartTooltip />
    <ChartLegend />
    <Line DataKey="desktop" Name="Desktop" Color="var(--chart-1)" />
    <Line DataKey="mobile" Name="Mobile" Color="var(--chart-2)" />
</LineChart>

@code {
    private record MonthlyData(string Month, int Desktop, int Mobile);

    private List<MonthlyData> data =
    [
        new("Jan", 186, 80),
        new("Feb", 305, 200),
        new("Mar", 237, 120),
        new("Apr", 73, 190),
        new("May", 209, 130),
        new("Jun", 214, 140)
    ];
}
```

### Stacked Bar Chart

Use `Stacked="true"` on series to stack them:

```razor
<BarChart Data="@data" Height="300px">
    <XAxis DataKey="month" />
    <YAxis />
    <ChartTooltip />
    <ChartLegend />
    <Bar DataKey="desktop" Name="Desktop" Color="var(--chart-1)" Stacked="true" />
    <Bar DataKey="mobile" Name="Mobile" Color="var(--chart-2)" Stacked="true" />
</BarChart>
```

### Donut with Center Label

```razor
<PieChart Data="@data" Height="300px">
    <ChartTooltip />
    <Pie DataKey="visitors" NameKey="browser" InnerRadius="60">
        <CenterLabel Title="Total" Value="1,234" />
    </Pie>
</PieChart>
```

### Gradient Fill

Use the `Fill` system for custom gradients:

```razor
<AreaChart Data="@data" Height="300px">
    <XAxis DataKey="month" />
    <YAxis />
    <ChartTooltip />
    <Area DataKey="desktop" Name="Desktop" Color="var(--chart-1)" Curve="CurveType.Smooth">
        <Fill>
            <LinearGradient Direction="GradientDirection.Vertical">
                <GradientStop Offset="0" Color="var(--chart-1)" Opacity="0.8" />
                <GradientStop Offset="1" Color="var(--chart-1)" Opacity="0.1" />
            </LinearGradient>
        </Fill>
    </Area>
</AreaChart>
```

### With ChartContainer

Use `ChartContainer` for a Card-like wrapper with consistent styling:

```razor
<ChartContainer>
    <div class="mb-4">
        <h3 class="text-lg font-medium">Monthly Sales</h3>
        <p class="text-sm text-muted-foreground">Desktop vs Mobile</p>
    </div>
    <LineChart Data="@data" Height="300px">
        <XAxis DataKey="month" />
        <YAxis />
        <ChartTooltip />
        <Line DataKey="desktop" Name="Desktop" Color="var(--chart-1)" />
    </LineChart>
</ChartContainer>
```

## Common Parameters

### ChartBase (inherited by all chart types)

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Data` | `object?` | `null` | Data source (any `IEnumerable`) |
| `Config` | `ChartConfig?` | `null` | Series label/color mapping |
| `Height` | `string` | `"350px"` | Chart height (CSS value) |
| `Width` | `string` | `"100%"` | Chart width (CSS value) |
| `Title` | `string?` | `null` | Chart title |
| `EnableAnimations` | `bool` | `true` | Enable/disable animations |
| `Class` | `string?` | `null` | Additional CSS classes |

### Chart-Specific Parameters

**BarChart:**
- `Horizontal` (`bool`, default: `false`) — Render bars horizontally

**RadarChart:**
- `Shape` (`RadarShape`, default: `Polygon`) — Polygon or Circle grid
- `IndicatorKey` (`string?`) — Property name for indicator labels
- `Indicators` (`IEnumerable<EChartsRadarIndicator>?`) — Explicit indicators
- `MaxValue` (`double?`) — Maximum value for all axes
- `ShowAxisLines` (`bool`, default: `true`) — Show lines from center to vertices
- `ShowGridLines` (`bool`, default: `true`) — Show grid ring lines
- `GridFill` (`bool`, default: `false`) — Fill areas between grid rings

**RadialBarChart:**
- `StartAngle` (`int`, default: `90`) — Starting angle in degrees
- `EndAngle` (`int`, default: `-270`) — Ending angle in degrees
- `InnerRadius` (`string`, default: `"30%"`) — Inner radius
- `OuterRadius` (`string`, default: `"80%"`) — Outer radius

`RoseChart` and `SankeyChart` take no parameters of their own; everything is set on the series.

## Composable Components

### XAxis / YAxis

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `DataKey` | `string?` | `null` | Property name for category values (XAxis) |
| `Type` | `AxisType` | `Category` / `Value` | Axis type |
| `Show` | `bool` | `true` | Visibility |
| `ShowAxisLine` | `bool` | `false` | Show the axis line |
| `ShowTicks` | `bool` | `false` | Show tick marks |
| `ShowGrid` | `bool` | `false` / `true` | Show grid lines |
| `GridColor` | `string?` | `null` | Grid line color |
| `GridType` | `LineStyleType` | `Dashed` | Grid line style |
| `LabelColor` | `string?` | `null` | Label text color |
| `LabelFontSize` | `int?` | `null` | Label font size |
| `LabelRotate` | `int?` | `null` | Label rotation angle |
| `Name` | `string?` | `null` | Axis name |
| `Min` | `object?` | `null` | Minimum value |
| `Max` | `object?` | `null` | Maximum value |

XAxis also has: `LabelInside` (`bool`), `BoundaryGap` (`bool?`).

### Grid

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Left` | `string` | `"12"` | Distance from left |
| `Right` | `string` | `"12"` | Distance from right |
| `Top` | `string` | `"0"` | Distance from top |
| `Bottom` | `string` | `"0"` | Distance from bottom |
| `ContainLabel` | `bool` | `true` | Grid area contains axis labels |

### ChartTooltip

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Indicator` | `TooltipIndicator` | `Dot` | Indicator style (Dot, Line, Dashed, None) |
| `BackgroundColor` | `string?` | `null` | Background color |
| `BorderColor` | `string?` | `null` | Border color |
| `TextColor` | `string?` | `null` | Text color |

### ChartLegend

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Position` | `LegendPosition` | `Bottom` | Position (Top, Bottom, Left, Right, Hidden) |
| `TextColor` | `string?` | `null` | Text color |
| `ItemGap` | `int` | `16` | Gap between items |

## Series Components

### Common (inherited from SeriesBase)

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `DataKey` | `string?` | `null` | Property name for series values |
| `Name` | `string?` | `null` | Display name |
| `Color` | `string?` | `null` | Series color (CSS color or `var()`) |
| `Stacked` | `bool` | `false` | Stack with other series |
| `StackGroup` | `string` | `"stack"` | Stack group identifier |

### Line

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Curve` | `CurveType` | `Linear` | Interpolation (Linear, Smooth, Step, StepBefore, StepAfter) |
| `StrokeWidth` | `int` | `2` | Line width in px |
| `ShowDots` | `bool` | `true` | Show data points |
| `DotSize` | `int` | `4` | Dot size in px |
| `Dashed` | `bool` | `false` | Dashed stroke |

### Bar

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `BorderRadius` | `int?` | `null` | Corner radius (4 for non-stacked, 0 for stacked) |
| `BarWidth` | `string?` | `null` | Width (% or px) |
| `ShowLabel` | `bool` | `false` | Show data labels |
| `LabelPosition` | `LabelPosition` | `Top` | Label position |
| `LabelFormatter` | `string?` | `null` | ECharts formatter string |
| `LabelColor` | `string?` | `null` | Label text color |
| `LabelFontSize` | `int?` | `null` | Label font size |
| `FillKey` | `string?` | `null` | Property for per-item fill colors |

### Area

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Curve` | `CurveType` | `Linear` | Interpolation |
| `StrokeWidth` | `int` | `2` | Line width in px |
| `FillOpacity` | `double` | `0.4` | Fill area opacity (0–1) |
| `ShowDots` | `bool` | `false` | Show data points |

### Pie

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `NameKey` | `string?` | `null` | Property for category names |
| `OuterRadius` | `int` | `80` | Outer radius (%) |
| `InnerRadius` | `int` | `0` | Inner radius (%) — >0 for donut |
| `PaddingAngle` | `int` | `2` | Angle between slices |
| `ShowLabels` | `bool` | `false` | Show labels |
| `LabelPosition` | `LabelPosition` | `Outside` | Label position |
| `LabelFormatter` | `string?` | `null` | ECharts formatter string |
| `LabelColor` | `string?` | `null` | Label text color |
| `LabelFontSize` | `int?` | `null` | Label font size |
| `ShowLabelLine` | `bool?` | `null` | Show leader lines |
| `ActiveIndex` | `int?` | `null` | Pre-selected slice index |

### Radar

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `FillOpacity` | `double` | `0.25` | Fill area opacity (0–1) |
| `ShowDots` | `bool` | `true` | Show data points |
| `DotSize` | `int` | `4` | Dot size in px |
| `StrokeWidth` | `int` | `2` | Line width in px |

### RadialBar

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `NameKey` | `string?` | `null` | Property for category names |
| `RoundCap` | `bool` | `true` | Round bar ends |
| `ShowLabels` | `bool` | `false` | Show value labels |
| `ShowBackground` | `bool` | `false` | Show background track |

### Rose

Everything `Pie` takes, plus:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Mode` | `RoseMode` | `Radius` | How a value maps onto a sector (`Radius`, `Area`) |

### Sankey

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `SourceKey` | `string?` | `null` | Property holding the name of the node each link leaves |
| `TargetKey` | `string?` | `null` | Property holding the name of the node each link enters |
| `Orientation` | `SankeyOrientation` | `Horizontal` | Direction the diagram flows in |
| `NodeAlign` | `SankeyNodeAlign` | `Justify` | Where nodes with no outgoing link sit (`Justify`, `Left`, `Right`) |
| `NodeWidth` | `int` | `20` | Node thickness in px |
| `NodeGap` | `int` | `8` | Gap between two nodes in the same column, in px |
| `Draggable` | `bool` | `false` | Whether a node can be dragged to a new position |
| `LinkColor` | `SankeyLinkColor` | `Gradient` | Ribbon colour (`Gradient`, `Source`, `Target`) |
| `LinkOpacity` | `double` | `0.4` | Ribbon opacity (0–1) |
| `Curveness` | `double` | `0.5` | How much the ribbons bow (0–1) |
| `FocusAdjacent` | `bool` | `true` | Hovering a node dims everything it is not connected to |
| `ShowLabels` | `bool` | `true` | Label each node with its name |
| `LabelPosition` | `LabelPosition` | `Right` | Label position relative to its node |
| `LabelFormatter` | `string?` | `null` | ECharts formatter string |
| `LabelColor` | `string?` | `null` | Label text color |
| `LabelFontSize` | `int?` | `null` | Label font size |

`NodeAlign` names a physical side rather than a reading-order one, because ECharts computes the
layout and does not mirror it under `dir="rtl"`.

### CenterLabel

Child of `Pie` or `RadialBar` for center text in donut/radial charts.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Title` | `string?` | `null` | Title text |
| `Value` | `string?` | `null` | Value text |
| `Text` | `string?` | `null` | Single text (overrides Title/Value) |
| `FontSize` | `int` | `24` | Font size in px |
| `FontWeight` | `string` | `"bold"` | Font weight |

## Fill System

Nest `Fill` inside any series to define custom gradients:

```razor
<Line DataKey="desktop" Color="var(--chart-1)">
    <Fill>
        <LinearGradient Direction="GradientDirection.Vertical">
            <GradientStop Offset="0" Color="var(--chart-1)" Opacity="0.8" />
            <GradientStop Offset="1" Color="var(--chart-1)" Opacity="0.1" />
        </LinearGradient>
    </Fill>
</Line>
```

| Component | Parameters |
|-----------|-----------|
| `Fill` | `ChildContent` |
| `LinearGradient` | `Direction` (`GradientDirection`, default: `Vertical`) |
| `GradientStop` | `Offset` (`double`, 0–1), `Color` (`string?`), `Opacity` (`double?`, 0–1) |

## Theming

Charts integrate with AtomBlazor's theme system via CSS custom properties:

```css
:root {
  --chart-1: oklch(0.646 0.222 41.116);
  --chart-2: oklch(0.6 0.118 184.704);
  --chart-3: oklch(0.398 0.07 227.392);
  --chart-4: oklch(0.828 0.189 84.429);
  --chart-5: oklch(0.769 0.188 70.08);
}

.dark {
  --chart-1: oklch(0.488 0.243 264.376);
  --chart-2: oklch(0.696 0.17 162.48);
  --chart-3: oklch(0.769 0.188 70.08);
  --chart-4: oklch(0.627 0.265 303.9);
  --chart-5: oklch(0.645 0.246 16.439);
}
```

Series auto-assign `--chart-1` through `--chart-5` in order when no explicit `Color` is set. To customize per-series:

```razor
<BarChart Data="@data" Config="@chartConfig" Height="300px">
    <XAxis DataKey="month" />
    <YAxis />
    <Bar DataKey="desktop" />
    <Bar DataKey="mobile" />
</BarChart>

@code {
    private ChartConfig chartConfig = ChartConfig.Create(
        ("desktop", new ChartSeriesConfig { Label = "Desktop Users", Color = "var(--chart-1)" }),
        ("mobile", new ChartSeriesConfig { Label = "Mobile Users", Color = "var(--chart-2)" })
    );
}
```

Since ECharts cannot natively consume CSS `var()` values, `chart-theme.js` resolves all variables to computed hex colors at runtime via `getComputedStyle()`, and converts OKLCH values using a canvas 2D context. A `MutationObserver` on `<html>` detects dark/light theme changes and triggers automatic re-rendering.

## Dependencies

- **Apache ECharts** (v5.5.x) — Bundled as a static web asset in `wwwroot/lib/echarts/echarts.min.js`
- No external CDN or NuGet charting packages required
