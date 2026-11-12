<div align="center">

# AtomBlazor

**shadcn/ui-grade UI components for Blazor — headless primitives, polished defaults, 100% .NET.**

*Build enterprise Blazor apps that look like Vercel shipped them.*

[![NuGet](https://img.shields.io/nuget/v/AtomBlazor.svg)](https://www.nuget.org/packages/AtomBlazor)
![.NET](https://img.shields.io/badge/.NET-11.0+-512BD4?logo=.net&logoColor=white)
![Trimmable](https://img.shields.io/badge/trim-AOT%20ready-success)
![Tailwind CSS](https://img.shields.io/badge/Tailwind_CSS-v4-38BDF8?logo=tailwindcss&logoColor=white)
![License](https://img.shields.io/badge/license-Apache--2.0-blue)

**[English](./README.md)** | [简体中文](./README.zh-CN.md)

</div>

---

## The whole library, at a glance

| Components | Headless primitives | Inline SVG icons | Stylesheets (gzip) | Automated tests | Demo pages |
| :---: | :---: | :---: | :---: | :---: | :---: |
| **474**<br>in 141 families | **128**<br>in 40 families | **5,393** | **< 30 KB** | **4,773** | **216** |

*Every number above is measured from this repository, not rounded up for the brochure. Component counts come straight from the public API baseline: 474 concrete `ComponentBase` types in `AtomBlazor`, 128 in `AtomBlazor.Primitives`.*

## Why AtomBlazor?

- **474 production-ready components in 141 families.** Buttons to DataGrids, Gantt charts, schedulers, pivot tables, charts and a rich-text editor — all styled in the clean, modern shadcn/ui visual language, powered by Tailwind CSS v4. Not a starter kit you finish yourself: a catalog you ship from.
- **A two-layer architecture.** Build your own design system on **AtomBlazor.Primitives** — 128 headless, fully composable primitives across 40 families, with built-in ARIA attributes and complete keyboard navigation — or ship today with the 474 pre-styled components built on top of them. Same engine, your brand.
- **Theming that actually works.** A single OKLCH design-token system drives light/dark modes, 5 base palettes, 17 primary colors, radius and density presets — every one switchable at runtime and persisted across reloads. No duplicate stylesheets, no `!important` wars, no rebuild.
- **Absurdly small for what it covers.** The stylesheet for all 474 components plus the full theme layer weigh **under 30 KB gzipped** (26.7 KB + 3.1 KB). One CSS payload, every component, every theme.
- **Runs everywhere Blazor runs.** Verified in CI on all four hosting models: Blazor Server, WebAssembly, Auto, and MAUI Blazor Hybrid. Not "should work" — built and tested on every commit.
- **Trimmable, zero trimming warnings.** Library projects enable `IsAotCompatible` and ship clean under ILLink analysis, guarded by a dedicated AOT smoke job. One caveat for **Native AOT**: the dynamic filtering and sorting paths compile to LINQ expression trees, which require dynamic code — pre-filter and pre-sort in memory (or use strongly-typed LINQ) when you deploy with Native AOT.
- **JS interop that stays out of the way.** Interactive modules are lazily imported on first render and cached per runtime with version stamps. Only the theme bootstrapper preloads. No npm at runtime, no bundler in your pipeline, no JS framework to learn.
- **5,393 icons, zero network requests.** Four icon packs — Lucide (1,753), Heroicons (1,288 across Outline/Solid/Mini/Micro), Feather (286), Font Awesome (2,066 across Solid/Regular/Brands) — compiled inline as SVG into the assembly. Zero HTTP round-trips, works offline, and each pack is a separate assembly — reference only the ones you use.
- **Localization is not an afterthought.** All 574 component strings route through a single `ILocalizer`. Override one key at startup, or subclass to wire `IStringLocalizer` and switch cultures at runtime.
- **Tested like infrastructure.** 4,302 C# tests (including rendered-markup snapshots) plus 471 JavaScript module tests — 4,773 automated checks standing between you and a regression.

## Requirements

> **[!IMPORTANT]**
> AtomBlazor targets **.NET 11 or later** (`net11.0`). It is **not compatible** with .NET 10, .NET 8, or any earlier version.

## Packages

| Package | NuGet | Description |
| --- | --- | --- |
| Components | [`AtomBlazor`](https://www.nuget.org/packages/AtomBlazor) | 474 pre-styled components in 141 families (depends on Primitives; bundles Markdig, HtmlSanitizer, TailwindMerge.NET, Ical.Net) |
| Primitives | [`AtomBlazor.Primitives`](https://www.nuget.org/packages/AtomBlazor.Primitives) | 128 headless, composable primitives in 40 families, with Portal / Positioning / Focus / Overlay services |
| Icons | [`AtomBlazor.Icons.Lucide`](https://www.nuget.org/packages/AtomBlazor.Icons.Lucide) | Lucide icons (1,753, inline SVG) |
| Icons | [`AtomBlazor.Icons.Heroicons`](https://www.nuget.org/packages/AtomBlazor.Icons.Heroicons) | Heroicons (1,288 — Outline / Solid / Mini / Micro) |
| Icons | [`AtomBlazor.Icons.Feather`](https://www.nuget.org/packages/AtomBlazor.Icons.Feather) | Feather icons (286) |
| Icons | [`AtomBlazor.Icons.FontAwesome`](https://www.nuget.org/packages/AtomBlazor.Icons.FontAwesome) | Font Awesome Free (2,066 — Solid / Regular / Brands) |

## Component Catalog

**141 component families. Zero gaps.** Grouped by what you're building, not by the alphabet. Every one shares the same OKLCH token theming, full keyboard support and ARIA semantics — learn one family and the rest follow the same conventions.
### Buttons & Actions

*Every click target you'll ever need.*

| Component | What it does |
| --- | --- |
| `Button` | The workhorse — five visual variants, loading and icon states, `AsChild` composition |
| `ButtonGroup` | Segmented actions that read as a single control |
| `SplitButton` | A primary action with a dropdown of alternatives |
| `Fab` | Floating action button for the one tap that matters |
| `Toggle` | Two-state pressed control |
| `Link` | Anchors with the full button design language |

### Text & Numeric Inputs

*Type anything, enforce everything.*

| Component | What it does |
| --- | --- |
| `Input` | Single-line text entry with adornments, character limits and validation-ready styling |
| `Textarea` | Auto-growing multi-line entry |
| `MaskedInput` | Pattern-enforced entry for phones, IDs, plates and more |
| `NumericInput` | Numbers with steppers and precision control |
| `CurrencyInput` | Money entry with currency symbols and thousand separators |
| `InputOTP` | One-time-code entry boxes |
| `InputGroup` | Compose inputs with attached buttons and dropdowns |
| `TagInput` | Type, hit enter, collect chips |

### Selection Controls

*From a simple switch to hierarchical cascades.*

| Component | What it does |
| --- | --- |
| `Select` | Custom dropdown with full keyboard navigation |
| `NativeSelect` | Native select element, styled to match everything else |
| `MultiSelect` | Pick-many dropdown with chips |
| `Combobox` | Type-ahead autocomplete dropdown |
| `ListBox` | Scrollable selectable list panel |
| `Checkbox` | Single checkbox with indeterminate state |
| `CheckboxGroup` | Grouped checkboxes with one binding |
| `RadioGroup` | Radio buttons with roving keyboard focus |
| `Switch` | The on/off control users already know |
| `Cascader` | Drill through hierarchical options level by level |
| `TreeSelect` | Select from expandable trees |
| `PickList` | Shuttle items between two lists |
| `Sortable` | Drag-to-reorder lists |

### Date & Time

*Pick a date, a range, a slot — in one gesture.*

| Component | What it does |
| --- | --- |
| `DatePicker` | Calendar popup with presets |
| `DateRangePicker` | Start–end dates in a single pick |
| `DateTimePicker` | Date and time in a single control |
| `DateInput` | Typeable date field with masking |
| `TimeInput` | Typeable time field |
| `TimePicker` | Dropdown time selection |

### Sliders, Files & More

*The inputs other libraries forget.*

| Component | What it does |
| --- | --- |
| `Slider` | Single-value slider with marks |
| `RangeSlider` | Dual-handle range selection |
| `Rating` | Star ratings, interactive or read-only |
| `QuantityStepper` | +/− stepper for quantities |
| `ColorPicker` | Palette and custom color selection |
| `FileUpload` | Drag & drop uploads with progress |
| `Attachment` | Attachment list with preview-friendly chips |
| `Signature` | Canvas signatures, touch and mouse |

### Forms & Validation

*From raw inputs to schema-driven forms, one line each.*

| Component | What it does |
| --- | --- |
| `Label` | Accessible form labels |
| `Field` | Label + hint + error layout shell |
| `InputField` | Input with field chrome in one tag |
| `FormField*` | 25 ready-made field wrappers — every input above pre-wired with label, hint, error text and validation state |
| `DynamicForm` | Schema-driven forms rendered from config |
| `FormWizard` | Multi-step forms with progress tracking |

### Overlays & Feedback

*Interrupt less; tell users more.*

| Component | What it does |
| --- | --- |
| `Dialog` | Modal conversations that trap focus and return it |
| `AlertDialog` | Destructive-action confirmations |
| `Sheet` | Edge-anchored sliding panels |
| `Drawer` | Slide-in navigation and content |
| `Popover` | Rich anchored overlays |
| `Tooltip` | Instant hover hints |
| `HoverCard` | Rich previews on hover |
| `ContextMenu` | Right-click menus anywhere |
| `DropdownMenu` | Action menus with submenus and checks |
| `Menubar` | Desktop-style menu bar |
| `Command` | Command palette for power users |
| `Toast` | Transient notifications that never block |
| `Message` | Inline status messages |
| `Alert` | Callouts for info, success, warning and danger |
| `NotificationBadge` | Counters and dots on any element |
| `ExitPrompt` | Confirm before users walk away from unsaved work |
| `Empty` | Beautiful empty states |
| `Skeleton` | Shimmer placeholders while loading |
| `Spinner` | Indeterminate loading indicator |
| `Progress` | Determinate progress bars |

### Navigation

*Every way to move through an app.*

| Component | What it does |
| --- | --- |
| `Tabs` | The tab navigation users expect |
| `Breadcrumb` | Hierarchy trail for deep pages |
| `Pagination` | Page controls for any list |
| `NavigationMenu` | Horizontal menu navigation |
| `BottomNav` | Mobile-style bottom navigation |
| `ResponsiveNav` | One component: desktop and mobile navigation |
| `Stepper` | Guided multi-step flows |
| `TreeView` | Expandable hierarchy browser |
| `Sidebar` | Full app shell with collapsible sections |
| `AppBar` | Top application bar |
| `Dock` | IDE-style dockable, maximizable panels |
| `ScrollToTop` | Back-to-top button that appears when needed |

### Data Display & Visualization

*From a badge to a Gantt chart — the whole ladder.*

| Component | What it does |
| --- | --- |
| `DataGrid` | The flagship grid — sorting, filtering, paging and more, client or server driven |
| `DataTable` | Lightweight table with the essentials |
| `DataView` | Toolbar-driven list/card data views |
| `PivotDataGrid` | Pivot grid for multi-dimensional summaries |
| `FilterBuilder` | Visual query builder for end users |
| `Calendar` | Month calendar with selection and optional lunar sub-labels |
| `EventCalendar` | Event calendar for date-backed content |
| `Scheduler` | Time-slot scheduling |
| `Gantt` | Gantt chart for project schedules |
| `Chart` | Interactive charts powered by Apache ECharts |
| `Timeline` | Timeline storytelling |
| `Accordion` | Expand/collapse sections |
| `Collapsible` | Single-section expand/collapse |
| `Avatar` | User avatars with fallbacks and groups |
| `Badge` | Status badges |
| `Chip` | Compact tag chips |
| `Kbd` | Keyboard key visuals |
| `Typography` | A complete typographic scale |
| `SectionHeader` | Consistent section titles |
| `Image` | Images with loading states and fallbacks |
| `Carousel` | Touch-friendly carousels |
| `Highlighter` | Highlight matched text segments |
| `CopyText` | One-click copy with visible confirmation |
| `Marker` | Compact inline markers that link |
| `Bubble` | Chat bubbles for conversational UI |

### Editors & Generators

*Heavyweight content, zero external services.*

| Component | What it does |
| --- | --- |
| `RichTextEditor` | Full WYSIWYG editing out of the box |
| `MarkdownEditor` | Markdown editing with live preview |
| `QrCode` | QR codes rendered locally — no service calls |
| `Barcode` | Barcodes rendered locally — no service calls |

### Layout

*Structure that stays out of your way.*

| Component | What it does |
| --- | --- |
| `Card` | The universal content container |
| `AspectRatio` | Lock media to the right shape |
| `Separator` | Hairline dividers, horizontal or vertical |
| `Resizable` | Drag to resize panes |
| `ScrollArea` | Custom-styled scroll areas |
| `DashboardGrid` | Draggable, resizable widget dashboards |
| `Item` | Composable media/content/actions card |

### Platform

*The machinery that makes everything else click.*

| Component | What it does |
| --- | --- |
| `Theme` | Runtime theming engine: palettes, radius, density, dark mode |
| `Motion` | Declarative entrance/exit animations |

## Quick Start

**1. Install the packages**

```bash
dotnet add package AtomBlazor
dotnet add package AtomBlazor.Icons.Lucide   # optional, pick your icon set
```

**2. Register the services** (`Program.cs`)

```csharp
builder.Services.AddAtomBlazorComponents(
    configureTheme: options =>
    {
        options.DefaultBaseColor = BaseColor.Zinc;
        options.DetectSystemPreference = true; // follow system light/dark
    });
```

Full signature: `AddAtomBlazorComponents(Action<DefaultLocalizer>? , Action<ThemeOptions>? , Action<OverlayRenderingOptions>?)`.

**3. Reference the styles** (in your host page's `<head>`) — styles ship as static assets with the library:

```html
<link href="_content/AtomBlazor/atomblazor.css" rel="stylesheet" />
<link href="_content/AtomBlazor/css/themes.css" rel="stylesheet" />
<script src="_content/AtomBlazor/js/theme-init.js"></script>
```

**4. Import the namespaces** (`_Imports.razor`)

```razor
@using AtomBlazor
@using AtomBlazor.Primitives
@using AtomBlazor.Icons.Lucide
```

**5. Use the components**

```razor
<Button Variant="ButtonVariant.Default" OnClick="@(() => count++)">Count: @count</Button>
<LucideIcon Name="house" Size="20" />
```

## Theming: one token system, every component

Change your entire app's personality at runtime — no stylesheet swap, no rebuild, no `!important` overrides.

```razor
@inject ThemeService Themes

<Button OnClick="@(() => Themes.SetPrimaryColorAsync(PrimaryColor.Violet))">Go violet</Button>
<Button OnClick="@(() => Themes.ToggleDarkModeAsync())">Toggle dark</Button>
<Button OnClick="@(() => Themes.SetPresetAsync(ThemePreset.Compact))">Compact density</Button>
```

| Dial | Options |
| --- | --- |
| Base palette | Zinc · Slate · Gray · Neutral · Stone |
| Primary color | Default + 17 named hues, Blue through Pink |
| Dark mode | Light · Dark · system-preference detection |
| Density preset | Standard · Compact · Dense · Spacious |
| Corner radius | Any value from 0 to 4 rem, applied live |

Every dial is independent: mix a Slate base with an Amber primary, Dense density and 0.75 rem radius. Choices persist to `localStorage` and are restored by a tiny blocking bootstrapper **before first paint** — dark-mode users never see a white flash, and your CSP needs no `unsafe-inline`. Full token reference: [THEMING.md](./THEMING.md).

## Localization: 574 strings, one interface

Every placeholder, button label, `aria-label` and empty-state message in all 474 components resolves through `ILocalizer`. Nothing is hard-coded into markup.

```csharp
// Pattern A — override individual keys at startup
builder.Services.AddAtomBlazorComponents(localizer =>
{
    localizer.Set("DataGrid.Loading", "Laden...");
    localizer.Set("Pagination.Next", "Weiter");
});

// Pattern B — subclass for runtime culture switching via IStringLocalizer
public class AppLocalizer(IStringLocalizer<SharedResources> loc) : DefaultLocalizer
{
    public override string this[string key] =>
        loc[key] is { ResourceNotFound: false } found ? found.Value : base[key];
}
```

English defaults ship in the box; keys use dotted notation (`DataGrid.Loading`) and standard `string.Format` placeholders (`"Showing {0}–{1} of {2}"`).

## Built like infrastructure

A UI library is load-bearing code. It is tested like it.

- **4,302 C# tests** — behavior, ARIA semantics, keyboard navigation and rendered-markup snapshots that fail the build when a single class name drifts.
- **471 JavaScript module tests** — the interop layer is tested as real code, not assumed to work, and gated in CI on every commit.
- **A public API baseline** — every component parameter, enum and service interface is snapshotted. An accidental breaking change cannot slip into a release.
- **An AOT smoke job** — trimming and Native AOT compatibility are verified in CI, not claimed in a README.
- **Four hosting models built on every commit** — Server, WebAssembly, Auto and MAUI Blazor Hybrid.
- **216 demo pages and 1,268 copy-paste examples** — run any project under `demos/` and browse the whole catalog live.

## Rendering Modes

AtomBlazor is not tied to a hosting model — it is verified in production on:

- **Blazor Server** (`demos/AtomBlazor.Demo.Server`)
- **Blazor WebAssembly** (`demos/AtomBlazor.Demo.Wasm`)
- **Auto** (`demos/AtomBlazor.Demo.Auto`)
- **MAUI Blazor Hybrid** (`demos/AtomBlazor.Demo.Maui` — proves Portal, lazy JS module loading and `_content/` static asset paths)

## Documentation

- **Theming guide** — [THEMING.md](./THEMING.md) *(Chinese)*
- **Contributing / build & test** — [CONTRIBUTING.md](./CONTRIBUTING.md) *(Chinese)*
- Live component browser: run any project under `demos/`

## License

Apache-2.0. Third-party icons and fonts follow their original licenses (Lucide/Heroicons/Feather = MIT, Font Awesome icons = SIL OFL / CC-BY-4.0) — see each package's `LICENSE` and `THIRD-PARTY-NOTICES.txt`, and the root [`LICENSE`](./LICENSE) for the full text.

> This project is intended for learning and further development. It is not an official product of shadcn/ui, Tailwind Labs, or any icon library listed above.
