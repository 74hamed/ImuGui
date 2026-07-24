using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ImuGui.Instruments;

/// <summary>
/// Owner-drawn heading indicator (directional gyro) avionics control.
/// Push new values via <see cref="HeadingDegrees"/>; the control invalidates
/// itself on every change.
/// </summary>
public sealed class HeadingIndicatorControl : InstrumentControl
{
    // ── Backing fields ───────────────────────────────────────────────────────
    private double _headingDegrees;

    // ── Palette constants ────────────────────────────────────────────────────
    private static readonly Color FaceColor = Color.FromArgb(18, 18, 22);
    private static readonly Color CardColor = Color.FromArgb(28, 28, 35);
    private static readonly Color TickColor = Color.FromArgb(220, 220, 220);
    private static readonly Color LabelColor = Color.FromArgb(200, 200, 200);
    private static readonly Color NorthColor = Color.FromArgb(255, 120, 60);
    private static readonly Color LubberColor = Color.FromArgb(240, 240, 240);
    private static readonly Color AircraftColor = Color.FromArgb(180, 180, 190);
    private static readonly Color BezelColor = Color.FromArgb(45, 45, 45);
    private static readonly Color BezelHighlight = Color.FromArgb(90, 90, 90);

    // ── Cardinal / intercardinal label table ─────────────────────────────────
    // Entries: (degrees, label, isCardinal)
    private static readonly (int Deg, string Label, bool IsCardinal)[] CompassLabels =
    [
        (0,   "N",  true),
        (30,  "3",  false),
        (60,  "6",  false),
        (90,  "E",  true),
        (120, "12", false),
        (150, "15", false),
        (180, "S",  true),
        (210, "21", false),
        (240, "24", false),
        (270, "W",  true),
        (300, "30", false),
        (330, "33", false),
    ];

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the magnetic heading in degrees.
    /// Any value is accepted; display normalises to [0, 360).
    /// </summary>
    [Category("Instrument")]
    [Description("Magnetic heading in degrees. Normalized to [0, 360) for display.")]
    [DefaultValue(0d)]
    public double HeadingDegrees
    {
        get => _headingDegrees;
        set
        {
            if (_headingDegrees == value)
            {
                return;
            }

            _headingDegrees = value;
            Invalidate();
        }
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override void RenderInstrument(Graphics g, float radius)
    {
        double heading = NormalizeHeading(_headingDegrees);

        float innerR = radius * 0.92f;

        // ── 1. Face background ────────────────────────────────────────────
        using var faceBrush = new SolidBrush(FaceColor);
        g.FillEllipse(faceBrush, -innerR, -innerR, innerR * 2, innerR * 2);

        // ── 2. Compass card (rotates so current heading is at top) ────────
        var cardState = g.Save();
        try
        {
            // Rotate card: heading at top means we rotate by -heading
            g.RotateTransform(-(float)heading);
            DrawCompassCard(g, innerR);
        }
        finally
        {
            g.Restore(cardState);
        }

        // ── 3. Fixed lubber line at top ───────────────────────────────────
        DrawLubberLine(g, innerR);

        // ── 4. Fixed aircraft silhouette in center ─────────────────────────
        DrawAircraftSilhouette(g, innerR);

        // ── 5. Bezel ring ─────────────────────────────────────────────────
        DrawBezel(g, radius, innerR);
    }

    // ── Section: compass card ────────────────────────────────────────────────

    private static void DrawCompassCard(Graphics g, float innerR)
    {
        float cardR = innerR * 0.95f;

        using var tickPen = new Pen(TickColor, 1.5f);
        using var labelBrush = new SolidBrush(LabelColor);
        using var northBrush = new SolidBrush(NorthColor);
        using var cardBrush = new SolidBrush(CardColor);

        // Thin card ring background
        using var cardPath = new GraphicsPath();
        cardPath.AddEllipse(-cardR, -cardR, cardR * 2, cardR * 2);
        float innerCardR = cardR * 0.62f;
        using var innerCardPath = new GraphicsPath();
        innerCardPath.AddEllipse(-innerCardR, -innerCardR, innerCardR * 2, innerCardR * 2);
        using var cardRegion = new Region(cardPath);
        cardRegion.Exclude(innerCardPath);
        g.FillRegion(cardBrush, cardRegion);

        float majorTickOuter = cardR;
        float majorTickInner = cardR * 0.88f;
        float minorTickOuter = cardR;
        float minorTickInner = cardR * 0.93f;

        // Ticks every 5 degrees, longer every 10
        for (int deg = 0; deg < 360; deg += 5)
        {
            bool isMajor = (deg % 10) == 0;
            float innerT = isMajor ? majorTickInner : minorTickInner;
            DrawTickAt(g, tickPen, deg, innerT, majorTickOuter);
        }

        // Labels every 30 degrees
        float labelR = cardR * 0.78f;
        float labelFontSize = Math.Max(6f, innerR * 0.10f);
        float cardinalFontSize = Math.Max(7f, innerR * 0.13f);

        foreach (var (deg, label, isCardinal) in CompassLabels)
        {
            float fontSize = isCardinal ? cardinalFontSize : labelFontSize;
            FontStyle style = isCardinal ? FontStyle.Bold : FontStyle.Regular;
            using var font = new Font("Arial", fontSize, style, GraphicsUnit.Pixel);
            Brush brush = (label == "N") ? northBrush : labelBrush;

            // Position: on the card at the given bearing angle
            double rad = deg * Math.PI / 180.0;
            float lx = (float)Math.Sin(rad) * labelR;
            float ly = -(float)Math.Cos(rad) * labelR;

            // Rotate label to stay readable (upright relative to card)
            var labelState = g.Save();
            try
            {
                g.TranslateTransform(lx, ly);
                // Labels on the card rotate with it but text is not counter-rotated
                DrawCenteredString(g, label, font, brush, 0, 0);
            }
            finally
            {
                g.Restore(labelState);
            }
        }
    }

    // ── Section: lubber line ─────────────────────────────────────────────────

    private static void DrawLubberLine(Graphics g, float innerR)
    {
        float lubberR = innerR * 0.93f;
        float triH = innerR * 0.10f;
        float triW = innerR * 0.07f;

        // Small downward-pointing triangle at top
        var tri = new PointF[]
        {
            new(0, -lubberR),
            new(-triW, -lubberR - triH),
            new(triW, -lubberR - triH),
        };
        using var lubberBrush = new SolidBrush(LubberColor);
        using var lubberPen = new Pen(Color.FromArgb(20, 20, 20), 1f);
        g.FillPolygon(lubberBrush, tri);
        g.DrawPolygon(lubberPen, tri);

        // Hairline from triangle apex down to inner card edge
        float hairlineEnd = innerR * 0.68f;
        using var hairPen = new Pen(LubberColor, 1.5f);
        g.DrawLine(hairPen, 0, -lubberR, 0, -hairlineEnd);
    }

    // ── Section: aircraft silhouette ─────────────────────────────────────────

    private static void DrawAircraftSilhouette(Graphics g, float innerR)
    {
        float scale = innerR * 0.28f;

        using var brush = new SolidBrush(AircraftColor);
        using var pen = new Pen(Color.FromArgb(60, 60, 70), 1f);

        // Fuselage: thin vertical rectangle
        float fw = scale * 0.12f;
        float fh = scale * 0.90f;
        var fuse = new RectangleF(-fw / 2, -fh * 0.6f, fw, fh);
        g.FillRectangle(brush, fuse);
        g.DrawRectangle(pen, fuse.X, fuse.Y, fuse.Width, fuse.Height);

        // Wings: wide horizontal rectangle centered slightly back
        float ww = scale * 1.0f;
        float wh = scale * 0.12f;
        float wingY = 0;
        var wing = new RectangleF(-ww / 2, wingY - wh / 2, ww, wh);
        g.FillRectangle(brush, wing);
        g.DrawRectangle(pen, wing.X, wing.Y, wing.Width, wing.Height);

        // Tail: small horizontal bar at back
        float tw = scale * 0.45f;
        float th = scale * 0.10f;
        float tailY = fh * 0.3f;
        var tail = new RectangleF(-tw / 2, tailY - th / 2, tw, th);
        g.FillRectangle(brush, tail);
        g.DrawRectangle(pen, tail.X, tail.Y, tail.Width, tail.Height);
    }

    // ── Section: bezel ───────────────────────────────────────────────────────

    private static void DrawBezel(Graphics g, float radius, float innerR)
    {
        float bezelWidth = radius - innerR;

        using var bezelBrush = new SolidBrush(BezelColor);
        using var highlightPen = new Pen(BezelHighlight, Math.Max(1f, bezelWidth * 0.25f));

        using var outerPath = new GraphicsPath();
        outerPath.AddEllipse(-radius, -radius, radius * 2, radius * 2);
        using var innerPath = new GraphicsPath();
        innerPath.AddEllipse(-innerR, -innerR, innerR * 2, innerR * 2);

        using var bezelRegion = new Region(outerPath);
        bezelRegion.Exclude(innerPath);
        g.FillRegion(bezelBrush, bezelRegion);

        g.DrawEllipse(highlightPen, -innerR, -innerR, innerR * 2, innerR * 2);

        using var rimPen = new Pen(Color.FromArgb(20, 20, 20), 2f);
        g.DrawEllipse(rimPen, -radius, -radius, radius * 2, radius * 2);
    }

    // ── Normalisation ────────────────────────────────────────────────────────

    private static double NormalizeHeading(double deg)
    {
        deg %= 360.0;
        if (deg < 0.0)
        {
            deg += 360.0;
        }

        return deg;
    }
}
