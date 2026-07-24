using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ImuGui.Instruments;

/// <summary>
/// Owner-drawn artificial horizon (attitude indicator) avionics control.
/// Push new values via <see cref="RollDegrees"/> and <see cref="PitchDegrees"/>;
/// the control invalidates itself on every change.
/// </summary>
public sealed class ArtificialHorizonControl : InstrumentControl
{
    // ── Backing fields ───────────────────────────────────────────────────────
    private double _rollDegrees;
    private double _pitchDegrees;

    // ── Palette constants ────────────────────────────────────────────────────
    private static readonly Color SkyTop = Color.FromArgb(30, 80, 160);
    private static readonly Color SkyBottom = Color.FromArgb(60, 130, 200);
    private static readonly Color GroundTop = Color.FromArgb(120, 80, 30);
    private static readonly Color GroundBottom = Color.FromArgb(80, 50, 15);
    private static readonly Color HorizonLine = Color.White;
    private static readonly Color LadderColor = Color.White;
    private static readonly Color AircraftColor = Color.FromArgb(255, 200, 50);
    private static readonly Color BezelColor = Color.FromArgb(45, 45, 45);
    private static readonly Color BezelHighlight = Color.FromArgb(90, 90, 90);

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Gets or sets the roll angle in degrees. Positive = right wing down.</summary>
    [Category("Instrument")]
    [Description("Roll angle in degrees. Positive values bank right (right wing down).")]
    [DefaultValue(0d)]
    public double RollDegrees
    {
        get => _rollDegrees;
        set
        {
            if (_rollDegrees == value)
            {
                return;
            }

            _rollDegrees = value;
            Invalidate();
        }
    }

    /// <summary>Gets or sets the pitch angle in degrees. Positive = nose up.</summary>
    [Category("Instrument")]
    [Description("Pitch angle in degrees. Positive values indicate nose-up attitude.")]
    [DefaultValue(0d)]
    public double PitchDegrees
    {
        get => _pitchDegrees;
        set
        {
            if (_pitchDegrees == value)
            {
                return;
            }

            _pitchDegrees = value;
            Invalidate();
        }
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override void RenderInstrument(Graphics g, float radius)
    {
        // Compute display values
        double roll = NormalizeRoll(_rollDegrees);
        double pitch = Math.Clamp(_pitchDegrees, -90.0, 90.0);

        float innerR = radius * 0.92f;  // instrument face radius (inside bezel)

        // ── 1. Clip to instrument circle ──────────────────────────────────
        var clipState = PushCircularClip(g, innerR);

        // ── 2. Sky/ground rotating disc ───────────────────────────────────
        DrawSkyGroundDisc(g, innerR, roll, pitch);

        // ── 3. Pitch ladder (rotates with disc) ───────────────────────────
        DrawPitchLadder(g, innerR, roll, pitch);

        g.Restore(clipState);

        // ── 4. Roll arc (fixed to instrument, marks rotate with arc) ──────
        DrawRollArc(g, innerR, roll);

        // ── 5. Fixed aircraft symbol ───────────────────────────────────────
        DrawAircraftSymbol(g, innerR);

        // ── 6. Bezel ring ─────────────────────────────────────────────────
        DrawBezel(g, radius, innerR);
    }

    // ── Section: sky/ground disc ─────────────────────────────────────────────

    private static void DrawSkyGroundDisc(
        Graphics g, float innerR, double rollDeg, double pitchDeg)
    {
        float pixelsPerDegree = innerR / 45f;
        float pitchShift = (float)pitchDeg * pixelsPerDegree;
        // Clamp shift so at least 20% of the disc remains on each half
        pitchShift = Math.Clamp(pitchShift, -innerR * 0.78f, innerR * 0.78f);

        var discState = g.Save();
        try
        {
            g.RotateTransform((float)rollDeg);

            // Fill the entire disc with sky first
            float d = innerR * 2;
            using var skyBrush = new LinearGradientBrush(
                new RectangleF(-innerR, -innerR, d, d),
                SkyTop, SkyBottom, LinearGradientMode.Vertical);
            g.FillEllipse(skyBrush, -innerR, -innerR, d, d);

            // Overlay ground below the (shifted) horizon
            using var groundPath = new GraphicsPath();
            groundPath.AddRectangle(new RectangleF(-innerR, -pitchShift, d, innerR * 2));
            using var groundBrush = new LinearGradientBrush(
                new RectangleF(-innerR, -pitchShift, d, innerR * 2 + 1),
                GroundTop, GroundBottom, LinearGradientMode.Vertical);
            g.FillPath(groundBrush, groundPath);

            // Horizon line
            using var horizonPen = new Pen(HorizonLine, 2f);
            g.DrawLine(horizonPen, -innerR, -pitchShift, innerR, -pitchShift);
        }
        finally
        {
            g.Restore(discState);
        }
    }

    // ── Section: pitch ladder ────────────────────────────────────────────────

    private static void DrawPitchLadder(
        Graphics g, float innerR, double rollDeg, double pitchDeg)
    {
        float pixelsPerDegree = innerR / 45f;
        float pitchShift = (float)pitchDeg * pixelsPerDegree;
        pitchShift = Math.Clamp(pitchShift, -innerR * 0.78f, innerR * 0.78f);

        float fontSize = Math.Max(6f, innerR * 0.085f);

        var ladderState = g.Save();
        try
        {
            g.RotateTransform((float)rollDeg);
            g.TranslateTransform(0, -pitchShift);

            using var pen = new Pen(LadderColor, 1.5f);
            using var brush = new SolidBrush(LadderColor);
            using var font = new Font("Arial", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);

            // Major lines every 10 degrees (labeled, ±30), minor every 5
            for (int deg = -30; deg <= 30; deg += 5)
            {
                if (deg == 0)
                {
                    continue;
                }

                float y = -deg * pixelsPerDegree;
                bool isMajor = (deg % 10) == 0;
                float halfLen = isMajor ? innerR * 0.30f : innerR * 0.15f;

                g.DrawLine(pen, -halfLen, y, halfLen, y);

                // Short end-caps angled downward toward ground
                float capLen = innerR * 0.06f;
                float capDir = deg > 0 ? 1f : -1f;
                g.DrawLine(pen, -halfLen, y, -halfLen, y + capLen * capDir);
                g.DrawLine(pen, halfLen, y, halfLen, y + capLen * capDir);

                // Labels on major lines
                if (isMajor)
                {
                    string label = Math.Abs(deg).ToString();
                    float labelX = halfLen + innerR * 0.06f;
                    DrawCenteredString(g, label, font, brush, labelX, y);
                    DrawCenteredString(g, label, font, brush, -labelX, y);
                }
            }
        }
        finally
        {
            g.Restore(ladderState);
        }
    }

    // ── Section: roll arc ────────────────────────────────────────────────────

    private static void DrawRollArc(Graphics g, float innerR, double rollDeg)
    {
        float arcR = innerR * 0.88f;
        float arcThickness = Math.Max(1.5f, innerR * 0.012f);

        using var pen = new Pen(Color.White, arcThickness);

        // Draw arc from -60 to +60 degrees (measured from top)
        g.DrawArc(pen,
            -arcR, -arcR, arcR * 2, arcR * 2,
            -150f, 120f);   // GDI+ angles: 0=right, -90=top; arc from -60 to +60 from top

        // Fixed tick marks at standard roll angles
        int[] majorAngles = [30, 60];
        int[] minorAngles = [10, 20, 45];

        foreach (int absAngle in majorAngles)
        {
            DrawRollTick(g, arcR, absAngle, innerR * 0.07f, pen);
            DrawRollTick(g, arcR, -absAngle, innerR * 0.07f, pen);
        }

        foreach (int absAngle in minorAngles)
        {
            DrawRollTick(g, arcR, absAngle, innerR * 0.04f, pen);
            DrawRollTick(g, arcR, -absAngle, innerR * 0.04f, pen);
        }

        // Zero tick (top): small downward triangle
        using var whiteBrush = new SolidBrush(Color.White);
        float triSize = innerR * 0.05f;
        float triY = -(arcR - innerR * 0.005f);
        var zeroTri = new PointF[]
        {
            new(0, triY),
            new(-triSize, triY - triSize * 1.8f),
            new(triSize, triY - triSize * 1.8f),
        };
        g.FillPolygon(whiteBrush, zeroTri);

        // Moving roll pointer: triangle that follows the roll angle
        var pointerState = g.Save();
        try
        {
            g.RotateTransform(-(float)rollDeg);
            float pY = -(arcR - innerR * 0.005f);
            float pSize = innerR * 0.05f;
            var rollTri = new PointF[]
            {
                new(0, pY + pSize * 0.3f),
                new(-pSize, pY + pSize * 0.3f + pSize * 1.6f),
                new(pSize, pY + pSize * 0.3f + pSize * 1.6f),
            };
            using var accentBrush = new SolidBrush(Color.FromArgb(255, 220, 80));
            g.FillPolygon(accentBrush, rollTri);
            using var accentPen = new Pen(Color.White, 1f);
            g.DrawPolygon(accentPen, rollTri);
        }
        finally
        {
            g.Restore(pointerState);
        }
    }

    private static void DrawRollTick(
        Graphics g, float arcR, int angleDeg, float tickLen, Pen pen)
    {
        double rad = angleDeg * Math.PI / 180.0;
        float sin = (float)Math.Sin(rad);
        float cos = (float)Math.Cos(rad);
        // Tick from arcR inward
        g.DrawLine(pen,
            sin * arcR, -cos * arcR,
            sin * (arcR - tickLen), -cos * (arcR - tickLen));
    }

    // ── Section: fixed aircraft symbol ───────────────────────────────────────

    private static void DrawAircraftSymbol(Graphics g, float innerR)
    {
        float w = innerR * 0.35f;   // wing half-span
        float thick = innerR * 0.05f; // bar thickness
        float barLen = innerR * 0.12f;

        using var brush = new SolidBrush(AircraftColor);
        using var pen = new Pen(Color.Black, 1f);

        // Left wing (horizontal bar + short downward stub)
        var leftWing = new RectangleF(-w - barLen, -thick / 2, barLen, thick);
        g.FillRectangle(brush, leftWing);
        g.DrawRectangle(pen, leftWing.X, leftWing.Y, leftWing.Width, leftWing.Height);

        // Left fuselage connector
        var leftConn = new RectangleF(-w, -thick / 2, w - thick * 0.8f, thick);
        g.FillRectangle(brush, leftConn);
        g.DrawRectangle(pen, leftConn.X, leftConn.Y, leftConn.Width, leftConn.Height);

        // Right wing
        var rightWing = new RectangleF(w, -thick / 2, barLen, thick);
        g.FillRectangle(brush, rightWing);
        g.DrawRectangle(pen, rightWing.X, rightWing.Y, rightWing.Width, rightWing.Height);

        // Right fuselage connector
        var rightConn = new RectangleF(thick * 0.8f, -thick / 2, w - thick * 0.8f, thick);
        g.FillRectangle(brush, rightConn);
        g.DrawRectangle(pen, rightConn.X, rightConn.Y, rightConn.Width, rightConn.Height);

        // Center dot
        float dotR = thick * 0.9f;
        g.FillEllipse(brush, -dotR, -dotR, dotR * 2, dotR * 2);
        g.DrawEllipse(pen, -dotR, -dotR, dotR * 2, dotR * 2);
    }

    // ── Section: bezel ───────────────────────────────────────────────────────

    private static void DrawBezel(Graphics g, float radius, float innerR)
    {
        float bezelWidth = radius - innerR;
        using var bezelBrush = new SolidBrush(BezelColor);
        using var bezelPen = new Pen(BezelHighlight, Math.Max(1f, bezelWidth * 0.25f));

        // Fill the ring between innerR and radius
        using var outerPath = new GraphicsPath();
        outerPath.AddEllipse(-radius, -radius, radius * 2, radius * 2);
        using var innerPath = new GraphicsPath();
        innerPath.AddEllipse(-innerR, -innerR, innerR * 2, innerR * 2);

        using var bezelRegion = new Region(outerPath);
        bezelRegion.Exclude(innerPath);
        g.FillRegion(bezelBrush, bezelRegion);

        // Highlight ring at the inner edge of the bezel
        g.DrawEllipse(bezelPen, -innerR, -innerR, innerR * 2, innerR * 2);

        // Outer rim
        using var rimPen = new Pen(Color.FromArgb(20, 20, 20), 2f);
        g.DrawEllipse(rimPen, -radius, -radius, radius * 2, radius * 2);
    }

    // ── Normalisation helpers ────────────────────────────────────────────────

    private static double NormalizeRoll(double deg)
    {
        // Wrap to (-180, 180]
        deg %= 360.0;
        if (deg > 180.0)
        {
            deg -= 360.0;
        }
        else if (deg <= -180.0)
        {
            deg += 360.0;
        }

        return deg;
    }
}
