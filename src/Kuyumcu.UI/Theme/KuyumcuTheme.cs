using MudBlazor;

namespace Kuyumcu.UI.Theme;

/// <summary>
/// Ürünün tasarım sistemi: antrasit + altın, sıcak nötr zeminler.
/// Giden (müşteri borçlu) kırmızı, Gelen (dükkân borçlu) yeşil — tüm ekranlarda aynı.
/// </summary>
public static class KuyumcuTheme
{
    public const string FontStack = "\"Segoe UI Variable Text\", \"Segoe UI\", Inter, -apple-system, BlinkMacSystemFont, Roboto, \"Helvetica Neue\", Arial, sans-serif";

    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#8A6416",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#2B2620",
            Tertiary = "#B8860B",
            Info = "#2F6FB0",
            Success = "#2E7D32",
            Error = "#C62828",
            Warning = "#C77700",
            Background = "#F6F4EF",
            BackgroundGray = "#EFECE5",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#2B2620",
            DrawerBackground = "#1F1A14",
            DrawerText = "rgba(255,255,255,0.78)",
            DrawerIcon = "rgba(255,255,255,0.62)",
            TextPrimary = "#231F1A",
            TextSecondary = "#6B645A",
            ActionDefault = "#6B645A",
            LinesDefault = "#E6E1D8",
            LinesInputs = "#CFC8BC",
            TableLines = "#EEEAE2",
            TableHover = "rgba(138,100,22,0.06)",
            TableStriped = "rgba(0,0,0,0.015)",
            Divider = "#E6E1D8",
            HoverOpacity = 0.06,
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#D9AE4A",
            PrimaryContrastText = "#1F1A14",
            Secondary = "#E9E2D6",
            Tertiary = "#D9AE4A",
            Info = "#6FA8DC",
            Success = "#66BB6A",
            Error = "#EF5350",
            Warning = "#FFB74D",
            Background = "#14110D",
            BackgroundGray = "#1B1713",
            Surface = "#1E1A15",
            AppbarBackground = "#1E1A15",
            AppbarText = "#EDE7DC",
            DrawerBackground = "#0F0D0A",
            DrawerText = "rgba(255,255,255,0.78)",
            DrawerIcon = "rgba(255,255,255,0.62)",
            TextPrimary = "#EDE7DC",
            TextSecondary = "#A79F92",
            ActionDefault = "#A79F92",
            LinesDefault = "#2E2820",
            LinesInputs = "#4A4236",
            TableLines = "#2A251E",
            TableHover = "rgba(217,174,74,0.08)",
            Divider = "#2E2820",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
            DrawerWidthLeft = "232px",
            DrawerMiniWidthLeft = "68px",
            AppbarHeight = "60px",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = FontStack.Split(','), FontSize = "0.9rem", LineHeight = "1.5", LetterSpacing = "0" },
            H4 = new H4Typography { FontFamily = FontStack.Split(','), FontSize = "1.75rem", FontWeight = "650", LetterSpacing = "-0.01em" },
            H5 = new H5Typography { FontFamily = FontStack.Split(','), FontSize = "1.35rem", FontWeight = "650", LetterSpacing = "-0.005em" },
            H6 = new H6Typography { FontFamily = FontStack.Split(','), FontSize = "1.05rem", FontWeight = "600", LetterSpacing = "0" },
            Subtitle1 = new Subtitle1Typography { FontFamily = FontStack.Split(','), FontSize = "0.95rem", FontWeight = "600" },
            Subtitle2 = new Subtitle2Typography { FontFamily = FontStack.Split(','), FontSize = "0.85rem", FontWeight = "600" },
            Body1 = new Body1Typography { FontFamily = FontStack.Split(','), FontSize = "0.92rem" },
            Body2 = new Body2Typography { FontFamily = FontStack.Split(','), FontSize = "0.85rem" },
            Button = new ButtonTypography { FontFamily = FontStack.Split(','), FontSize = "0.875rem", FontWeight = "600", TextTransform = "none", LetterSpacing = "0" },
            Caption = new CaptionTypography { FontFamily = FontStack.Split(','), FontSize = "0.78rem" },
            Overline = new OverlineTypography { FontFamily = FontStack.Split(','), FontSize = "0.7rem", FontWeight = "600", LetterSpacing = "0.08em" },
        },
    };
}
