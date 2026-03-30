# SNAPP Theme Specification

## MudTheme Configuration

```csharp
new MudTheme
{
    PaletteLight = new PaletteLight
    {
        Primary = "#1B3A5C",        // Deep professional blue — brand anchor
        Secondary = "#4A90A4",      // Teal accent — interactive elements, links
        Tertiary = "#6B8E5A",       // Muted green — positive indicators, success
        AppbarBackground = "#FFFFFF",
        AppbarText = "#1B3A5C",
        DrawerBackground = "#F5F7FA",
        DrawerText = "#374151",
        Background = "#FAFBFC",
        Surface = "#FFFFFF",
        Error = "#DC3545",
        Warning = "#F59E0B",
        Success = "#059669",
        Info = "#4A90A4",
        TextPrimary = "#111827",
        TextSecondary = "#6B7280",
        ActionDefault = "#6B7280",
        ActionDisabled = "#D1D5DB",
        ActionDisabledBackground = "#F3F4F6",
        Divider = "#E5E7EB",
        DividerLight = "#F3F4F6",
        TableHover = "rgba(27, 58, 92, 0.04)",
        TableStriped = "rgba(27, 58, 92, 0.02)",
    },
    PaletteDark = new PaletteDark
    {
        Primary = "#5B9BD5",
        Secondary = "#6BC0D6",
        Tertiary = "#8FBF7A",
        AppbarBackground = "#1E1E2E",
        AppbarText = "#E0E0E0",
        DrawerBackground = "#252536",
        DrawerText = "#C0C0C0",
        Background = "#1A1A2E",
        Surface = "#252536",
        Error = "#FF6B6B",
        Warning = "#FFD93D",
        Success = "#6BCB77",
        Info = "#6BC0D6",
        TextPrimary = "#E0E0E0",
        TextSecondary = "#9CA3AF",
    },
    Typography = new Typography
    {
        Default = new Default { FontFamily = new[] { "Inter", "system-ui", "sans-serif" } },
        H1 = new H1 { FontSize = "1.75rem", FontWeight = 700, LineHeight = 1.2 },
        H2 = new H2 { FontSize = "1.5rem", FontWeight = 600, LineHeight = 1.3 },
        H3 = new H3 { FontSize = "1.25rem", FontWeight = 600, LineHeight = 1.35 },
        H4 = new H4 { FontSize = "1.125rem", FontWeight = 600, LineHeight = 1.4 },
        H5 = new H5 { FontSize = "1rem", FontWeight = 600, LineHeight = 1.4 },
        H6 = new H6 { FontSize = "0.875rem", FontWeight = 600, LineHeight = 1.5 },
        Body1 = new Body1 { FontSize = "0.9375rem", LineHeight = 1.6 },
        Body2 = new Body2 { FontSize = "0.875rem", LineHeight = 1.5 },
        Caption = new Caption { FontSize = "0.75rem", LineHeight = 1.4 },
        Subtitle1 = new Subtitle1 { FontSize = "1rem", FontWeight = 500 },
        Subtitle2 = new Subtitle2 { FontSize = "0.875rem", FontWeight = 500 },
        Button = new Button { FontSize = "0.875rem", FontWeight = 600, TextTransform = "none" },
    },
    LayoutProperties = new LayoutProperties
    {
        DefaultBorderRadius = "6px",
    }
}
```

---

## Color Usage Guidelines

### Semantic Color Roles

| Role | Color | Hex | Usage |
|------|-------|-----|-------|
| **Primary** | Deep Blue | `#1B3A5C` | Primary buttons, selected nav items, active states, page headings |
| **Secondary** | Teal | `#4A90A4` | Links, secondary buttons, info badges, chart accents |
| **Tertiary** | Muted Green | `#6B8E5A` | Positive trends, growth indicators, completed states |
| **Error** | Red | `#DC3545` | Validation errors, destructive actions, alert banners |
| **Warning** | Amber | `#F59E0B` | Caution states, expiring items, medium-risk flags |
| **Success** | Green | `#059669` | Confirmed states, successful operations, positive scores |
| **Info** | Teal | `#4A90A4` | Informational tooltips, help text, neutral badges |

### Score and Metric Colors

For practice intelligence dashboards and scoring displays:

| Score Range | Color | Usage |
|-------------|-------|-------|
| 80-100 | `#059669` (Success) | High-performing metrics |
| 60-79 | `#4A90A4` (Info/Secondary) | Average-performing metrics |
| 40-59 | `#F59E0B` (Warning) | Below-average, attention needed |
| 0-39 | `#DC3545` (Error) | Critical, action required |

### Confidence Level Colors

| Confidence | Color | Badge Text |
|------------|-------|------------|
| High (>80%) | `#059669` | "High confidence" |
| Medium (50-80%) | `#F59E0B` | "Moderate confidence" |
| Low (<50%) | `#6B7280` | "Limited data" |

### Chart Palette (ordered)

For multi-series charts and radar/spider plots:

```
#1B3A5C, #4A90A4, #6B8E5A, #F59E0B, #DC3545, #8B5CF6
```

---

## Typography Scale

| Token | Size | Weight | Use |
|-------|------|--------|-----|
| H1 | 1.75rem (28px) | 700 | Page titles |
| H2 | 1.5rem (24px) | 600 | Section headings |
| H3 | 1.25rem (20px) | 600 | Card titles, panel headers |
| H4 | 1.125rem (18px) | 600 | Subsection headings |
| H5 | 1rem (16px) | 600 | Widget titles |
| H6 | 0.875rem (14px) | 600 | Label headings, small card titles |
| Body1 | 0.9375rem (15px) | 400 | Primary body text, form content |
| Body2 | 0.875rem (14px) | 400 | Secondary text, table cells |
| Caption | 0.75rem (12px) | 400 | Timestamps, helper text, badges |
| Subtitle1 | 1rem (16px) | 500 | Card subtitles, emphasized body |
| Subtitle2 | 0.875rem (14px) | 500 | List item secondary lines |
| Button | 0.875rem (14px) | 600 | All button labels |

### Font Loading

Primary: **Inter** via Google Fonts or self-hosted. Fallback: `system-ui, sans-serif`.

Add to `wwwroot/index.html`:
```html
<link rel="preconnect" href="https://fonts.googleapis.com">
<link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet">
```

---

## Spacing System

MudBlazor uses a 4px base unit. Standard spacing tokens:

| Token | Value | Usage |
|-------|-------|-------|
| `pa-1` / `ma-1` | 4px | Tight internal padding (badge content) |
| `pa-2` / `ma-2` | 8px | Compact card padding, icon gaps |
| `pa-3` / `ma-3` | 12px | Default element spacing |
| `pa-4` / `ma-4` | 16px | Card body padding, form field gaps |
| `pa-5` / `ma-5` | 20px | Section spacing |
| `pa-6` / `ma-6` | 24px | Page-level padding, major sections |
| `pa-8` / `ma-8` | 32px | Hero sections, major layout gaps |

### Standard Component Spacing

- **Card inner padding**: `pa-4` (16px)
- **Card gap in grids**: `ma-3` (12px)
- **Form field vertical gap**: `mb-4` (16px)
- **Section gap**: `mb-6` (24px)
- **Page top padding**: `pt-6` (24px)
- **AppBar height**: 64px (default)
- **Drawer width**: 280px (desktop), full-width overlay (mobile)

---

## Component Border Radius

| Component | Radius | Notes |
|-----------|--------|-------|
| Default (cards, dialogs, paper) | 6px | Set via `LayoutProperties.DefaultBorderRadius` |
| Buttons | 6px | Inherits from default |
| Chips / Badges | 16px | Use `Style="border-radius: 16px"` for pill shape |
| Avatars | 50% | MudAvatar default (circular) |
| Text inputs | 6px | Inherits from default |

---

## Elevation / Shadow

| Level | Usage | MudBlazor Prop |
|-------|-------|----------------|
| 0 | Flush elements, table rows | `Elevation="0"` |
| 1 | Cards at rest | `Elevation="1"` |
| 2 | Cards on hover, dropdowns | `Elevation="2"` |
| 4 | Floating panels, popovers | `Elevation="4"` |
| 8 | Dialogs, modals | `Elevation="8"` |

---

## Component Defaults

### Buttons

| Variant | MudBlazor Props | Usage |
|---------|-----------------|-------|
| Primary action | `<MudButton Variant="Variant.Filled" Color="Color.Primary">` | Submit, save, create |
| Secondary action | `<MudButton Variant="Variant.Outlined" Color="Color.Primary">` | Cancel, back, alternative |
| Tertiary / link | `<MudButton Variant="Variant.Text" Color="Color.Primary">` | Skip, dismiss, subtle actions |
| Destructive | `<MudButton Variant="Variant.Filled" Color="Color.Error">` | Delete, remove, revoke |

All buttons use `TextTransform="none"` (sentence case, not uppercase).

### Cards

```razor
<MudCard Elevation="1" Class="pa-4">
    <MudCardHeader>
        <CardHeaderContent>
            <MudText Typo="Typo.h6">Card Title</MudText>
        </CardHeaderContent>
    </MudCardHeader>
    <MudCardContent>
        <!-- Body2 for content text -->
    </MudCardContent>
</MudCard>
```

### Data Tables

- `Hover="true"` — row hover highlight
- `Striped="true"` — alternate row shading
- `Dense="true"` — compact rows for information-dense views
- `Bordered="false"` — clean, borderless aesthetic

### Form Fields

- `Variant="Variant.Outlined"` — all text inputs, selects, date pickers
- `Margin="Margin.Dense"` — tighter vertical spacing in forms
- Always include `Label` and `HelperText` where applicable
- Error text via `Error="true"` and `ErrorText="..."` (not custom markup)
