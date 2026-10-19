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

## Why AtomBlazor?

- **141 production-ready components.** Buttons to DataGrids, Gantt charts, schedulers, pivot tables, charts and a rich-text editor — all styled in the clean, modern shadcn/ui visual language, powered by Tailwind CSS v4.
- **A two-layer architecture.** Build your own design system on **AtomBlazor.Primitives** — headless, fully composable primitives with built-in ARIA attributes and complete keyboard navigation — or ship today with the pre-styled components built on top of them.
- **Theming that actually works.** A single OKLCH-based design-token system drives light/dark modes, 5 base palettes, 10+ primary colors, radius and density presets — every one switchable at runtime and persisted across reloads. No duplicate stylesheets, no `!important` wars.
- **Runs everywhere Blazor runs.** Verified on all four hosting models: Blazor Server, WebAssembly, Auto, and MAUI Blazor Hybrid.
- **Trimmable, zero trimming warnings.** Library projects enable `IsAotCompatible` and ship clean under ILLink analysis. One caveat for **Native AOT**: the dynamic filtering and sorting paths compile to LINQ expression trees, which require dynamic code — pre-filter and pre-sort in memory (or use strongly-typed LINQ) when you deploy with Native AOT.
- **JS interop that stays out of the way.** Interactive modules are lazily imported on first render and cached per runtime with version stamps. Only the theme bootstrapper preloads.
- **Icons without network requests.** Four icon packs — Lucide (1,753 icons), Heroicons (Outline/Solid/Mini/Micro), Feather (286), Font Awesome (Solid/Regular/Brands) — compiled inline as SVG into the assembly. Zero HTTP round-trips, works offline.

## Requirements

> **[!IMPORTANT]**
> AtomBlazor targets **.NET 11 or later** (`net11.0`). It is **not compatible** with .NET 10, .NET 8, or any earlier version.

## Packages

| Package | NuGet | Description |
| --- | --- | --- |
| Components | [`AtomBlazor`](https://www.nuget.org/packages/AtomBlazor) | 141 pre-styled components (depends on Primitives; bundles Markdig, HtmlSanitizer, TailwindMerge.NET, Ical.Net) |
| Primitives | [`AtomBlazor.Primitives`](https://www.nuget.org/packages/AtomBlazor.Primitives) | Headless, composable primitives with Portal / Positioning / Focus / Overlay services |
| Icons | [`AtomBlazor.Icons.Lucide`](https://www.nuget.org/packages/AtomBlazor.Icons.Lucide) | Lucide icons (1,753, inline SVG) |
| Icons | [`AtomBlazor.Icons.Heroicons`](https://www.nuget.org/packages/AtomBlazor.Icons.Heroicons) | Heroicons (Outline / Solid / Mini / Micro) |
| Icons | [`AtomBlazor.Icons.Feather`](https://www.nuget.org/packages/AtomBlazor.Icons.Feather) | Feather icons (286) |
| Icons | [`AtomBlazor.Icons.FontAwesome`](https://www.nuget.org/packages/AtomBlazor.Icons.FontAwesome) | Font Awesome (Solid / Regular / Brands) |

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
