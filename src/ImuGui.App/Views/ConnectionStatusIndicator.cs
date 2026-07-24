using ImuGui.Core.Models;

namespace ImuGui.App.Views;

/// <summary>
/// A colored lamp + text reflecting the REAL connection state of the active source —
/// green only when samples can actually flow, never a cosmetic "connected".
/// </summary>
public sealed class ConnectionStatusIndicator : Control
{
    private SensorConnectionState _state = SensorConnectionState.Disconnected;
    private string _stateText = "Disconnected";

    public ConnectionStatusIndicator()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer,
            true);
        Height = 24;
        Width = 320;
    }

    /// <summary>Updates the lamp color and text.</summary>
    public void SetState(SensorConnectionState state, string sourceDisplayName)
    {
        _state = state;
        _stateText = $"{StateLabel(state)} — {sourceDisplayName}";
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        const int lampDiameter = 12;
        int lampY = (Height - lampDiameter) / 2;
        using (var lampBrush = new SolidBrush(StateColor(_state)))
        {
            e.Graphics.FillEllipse(lampBrush, 2, lampY, lampDiameter, lampDiameter);
        }

        using var outline = new Pen(Color.FromArgb(80, 80, 80));
        e.Graphics.DrawEllipse(outline, 2, lampY, lampDiameter, lampDiameter);

        var textBounds = new Rectangle(lampDiameter + 8, 0, Width - lampDiameter - 8, Height);
        TextRenderer.DrawText(
            e.Graphics, _stateText, Font, textBounds, ForeColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    private static Color StateColor(SensorConnectionState state) => state switch
    {
        SensorConnectionState.Connected => Color.FromArgb(60, 180, 75),
        SensorConnectionState.Connecting => Color.FromArgb(255, 165, 0),
        SensorConnectionState.Reconnecting => Color.FromArgb(255, 165, 0),
        SensorConnectionState.Faulted => Color.FromArgb(215, 40, 40),
        _ => Color.FromArgb(150, 150, 150),
    };

    private static string StateLabel(SensorConnectionState state) => state switch
    {
        SensorConnectionState.Connected => "Connected",
        SensorConnectionState.Connecting => "Connecting…",
        SensorConnectionState.Reconnecting => "Reconnecting…",
        SensorConnectionState.Faulted => "Faulted",
        _ => "Disconnected",
    };
}
