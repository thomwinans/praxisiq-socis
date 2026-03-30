using MudBlazor;

namespace Snapp.Client.Theme;

public static class SnappTheme
{
    public static MudTheme Create() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1B3A5C",
            Secondary = "#4A90A4",
            Tertiary = "#6B8E5A",
            Error = "#DC3545",
            Warning = "#F59E0B",
            Success = "#059669",
            Info = "#4A90A4",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#1B3A5C",
            DrawerBackground = "#F5F7FA",
            DrawerText = "#374151",
            Background = "#FAFBFC",
            Surface = "#FFFFFF",
            TextPrimary = "#111827",
            TextSecondary = "#6B7280",
            TableHover = "rgba(27, 58, 92, 0.04)",
            TableStriped = "rgba(27, 58, 92, 0.02)",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#5B9BD5",
            Secondary = "#6BC0D6",
            Tertiary = "#8FBF7A",
            Error = "#DC3545",
            Warning = "#F59E0B",
            Success = "#059669",
            Info = "#6BC0D6",
            AppbarBackground = "#1E1E2E",
            AppbarText = "#E0E0E0",
            DrawerBackground = "#252536",
            DrawerText = "#C0C0C0",
            Background = "#1A1A2E",
            Surface = "#252536",
            TextPrimary = "#E0E0E0",
            TextSecondary = "#9CA3AF",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = new[] { "Inter", "system-ui", "-apple-system", "sans-serif" },
            },
            H1 = new H1Typography
            {
                FontSize = "1.75rem",
                FontWeight = "700",
            },
            H2 = new H2Typography
            {
                FontSize = "1.5rem",
                FontWeight = "600",
            },
            H3 = new H3Typography
            {
                FontSize = "1.25rem",
                FontWeight = "600",
            },
            H4 = new H4Typography
            {
                FontSize = "1.125rem",
                FontWeight = "600",
            },
            H5 = new H5Typography
            {
                FontSize = "1rem",
                FontWeight = "600",
            },
            H6 = new H6Typography
            {
                FontSize = "0.875rem",
                FontWeight = "600",
            },
            Body1 = new Body1Typography
            {
                FontSize = "0.9375rem",
                FontWeight = "400",
            },
            Body2 = new Body2Typography
            {
                FontSize = "0.875rem",
                FontWeight = "400",
            },
            Caption = new CaptionTypography
            {
                FontSize = "0.75rem",
                FontWeight = "400",
            },
            Subtitle1 = new Subtitle1Typography
            {
                FontSize = "1rem",
                FontWeight = "500",
            },
            Subtitle2 = new Subtitle2Typography
            {
                FontSize = "0.875rem",
                FontWeight = "500",
            },
            Button = new ButtonTypography
            {
                FontSize = "0.875rem",
                FontWeight = "600",
                TextTransform = "none",
            },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "6px",
            DrawerWidthLeft = "280px",
            AppbarHeight = "64px",
        },
    };
}
