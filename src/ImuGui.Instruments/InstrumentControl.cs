using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ImuGui.Instruments;

/// <summary>
/// Abstract base for owner-drawn avionics instrument controls.
/// Subclasses implement <see cref="RenderInstrument"/> to paint inside a
/// centered square whose half-side is <c>radius</c>, with the Graphics origin
/// already translated to the center of that square.
/// </summary>
public abstract class InstrumentControl : Control
{
    /// <summary>Initializes paint styles for flicker-free, anti-aliased rendering.</summary>
    protected InstrumentControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        DoubleBuffered = true;
    }

    /// <inheritdoc/>
    protected override Size DefaultSize => new(220, 220);

    /// <summary>
    /// Sealed paint handler: fills back color, skips degenerate sizes,
    /// configures anti-aliasing, computes the centered square, translates to its
    /// center, then delegates to <see cref="RenderInstrument"/>.
    /// </summary>
    protected sealed override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);

        if (Width < 8 || Height < 8)
        {
            return;
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        int side = Math.Min(Width, Height);
        int x = (Width - side) / 2;
        int y = (Height - side) / 2;
        float radius = side / 2f;

        var state = g.Save();
        try
        {
            g.TranslateTransform(x + radius, y + radius);
            RenderInstrument(g, radius);
        }
        finally
        {
            g.Restore(state);
        }
    }

    /// <summary>
    /// Subclasses render the instrument here. The Graphics origin is at the
    /// center of the largest centered square; <paramref name="radius"/> is half
    /// its side length.
    /// </summary>
    /// <param name="graphics">Configured Graphics with anti-aliasing enabled.</param>
    /// <param name="radius">Half the side of the centered square (pixels).</param>
    protected abstract void RenderInstrument(Graphics graphics, float radius);

    // ── Protected static helpers ────────────────────────────────────────────

    /// <summary>
    /// Pushes a circular clip region centered on the Graphics origin with the
    /// given radius, returning the saved state. Caller should restore when done.
    /// </summary>
    protected static GraphicsState PushCircularClip(Graphics g, float radius)
    {
        var state = g.Save();
        using var path = new GraphicsPath();
        path.AddEllipse(-radius, -radius, radius * 2, radius * 2);
        g.SetClip(path);
        return state;
    }

    /// <summary>
    /// Draws a single tick mark as a radial line from <paramref name="innerR"/>
    /// to <paramref name="outerR"/> at <paramref name="angleDeg"/> degrees
    /// (0 = top, positive clockwise).
    /// </summary>
    protected static void DrawTickAt(
        Graphics g, Pen pen, float angleDeg, float innerR, float outerR)
    {
        double rad = angleDeg * Math.PI / 180.0;
        float sin = (float)Math.Sin(rad);
        float cos = (float)Math.Cos(rad);
        g.DrawLine(pen,
            sin * innerR, -cos * innerR,
            sin * outerR, -cos * outerR);
    }

    /// <summary>
    /// Measures <paramref name="text"/> with <paramref name="font"/> and draws
    /// it centered on (<paramref name="cx"/>, <paramref name="cy"/>).
    /// </summary>
    protected static void DrawCenteredString(
        Graphics g, string text, Font font, Brush brush, float cx, float cy)
    {
        var size = g.MeasureString(text, font);
        g.DrawString(text, font, brush, cx - size.Width / 2f, cy - size.Height / 2f);
    }
}
