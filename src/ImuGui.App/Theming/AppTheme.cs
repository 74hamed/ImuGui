namespace ImuGui.App.Theming;

/// <summary>An application color palette. Two built-ins: <see cref="Dark"/> (default) and <see cref="Light"/>.</summary>
public sealed record AppTheme
{
    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>True for dark palettes (drives the title-bar mode).</summary>
    public required bool IsDark { get; init; }

    /// <summary>Base window background.</summary>
    public required Color WindowBackground { get; init; }

    /// <summary>Raised surfaces: group boxes, status bar, cards.</summary>
    public required Color SurfaceBackground { get; init; }

    /// <summary>Editable fields: text boxes, combos, numeric inputs.</summary>
    public required Color InputBackground { get; init; }

    /// <summary>Primary text.</summary>
    public required Color PrimaryText { get; init; }

    /// <summary>De-emphasized text (hints, inactive navigation).</summary>
    public required Color SecondaryText { get; init; }

    /// <summary>Accent for the primary action and active navigation.</summary>
    public required Color Accent { get; init; }

    /// <summary>Text on accent surfaces.</summary>
    public required Color AccentText { get; init; }

    /// <summary>Hairlines: separators, flat button borders.</summary>
    public required Color Border { get; init; }

    /// <summary>Chart figure (outer) background.</summary>
    public required Color ChartFigureBackground { get; init; }

    /// <summary>Chart data-area background.</summary>
    public required Color ChartDataBackground { get; init; }

    /// <summary>Chart grid lines.</summary>
    public required Color ChartGrid { get; init; }

    /// <summary>Chart axis frames, ticks, and labels.</summary>
    public required Color ChartAxisText { get; init; }

    /// <summary>The default dark palette.</summary>
    public static AppTheme Dark { get; } = new()
    {
        Name = "Dark",
        IsDark = true,
        WindowBackground = Color.FromArgb(27, 28, 32),
        SurfaceBackground = Color.FromArgb(36, 38, 44),
        InputBackground = Color.FromArgb(47, 50, 58),
        PrimaryText = Color.FromArgb(230, 231, 235),
        SecondaryText = Color.FromArgb(151, 154, 166),
        Accent = Color.FromArgb(62, 123, 250),
        AccentText = Color.White,
        Border = Color.FromArgb(58, 61, 70),
        ChartFigureBackground = Color.FromArgb(36, 38, 44),
        ChartDataBackground = Color.FromArgb(31, 33, 38),
        ChartGrid = Color.FromArgb(51, 54, 63),
        ChartAxisText = Color.FromArgb(201, 203, 212),
    };

    /// <summary>The light palette.</summary>
    public static AppTheme Light { get; } = new()
    {
        Name = "Light",
        IsDark = false,
        WindowBackground = Color.FromArgb(243, 244, 246),
        SurfaceBackground = Color.White,
        InputBackground = Color.White,
        PrimaryText = Color.FromArgb(30, 31, 36),
        SecondaryText = Color.FromArgb(107, 110, 120),
        Accent = Color.FromArgb(37, 99, 235),
        AccentText = Color.White,
        Border = Color.FromArgb(211, 213, 220),
        ChartFigureBackground = Color.White,
        ChartDataBackground = Color.FromArgb(251, 251, 253),
        ChartGrid = Color.FromArgb(228, 230, 235),
        ChartAxisText = Color.FromArgb(58, 61, 70),
    };
}
