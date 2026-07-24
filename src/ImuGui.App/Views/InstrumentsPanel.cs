using ImuGui.Core.Pipeline;
using ImuGui.Instruments;
using Orientation = ImuGui.Core.Models.Orientation;

namespace ImuGui.App.Views;

/// <summary>The aircraft instruments: artificial horizon (roll + pitch) and heading indicator (yaw).</summary>
public sealed class InstrumentsPanel : UserControl
{
    private readonly ArtificialHorizonControl _artificialHorizon;
    private readonly HeadingIndicatorControl _headingIndicator;

    public InstrumentsPanel()
    {
        Dock = DockStyle.Fill;
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(8),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _artificialHorizon = new ArtificialHorizonControl { Dock = DockStyle.Fill };
        _headingIndicator = new HeadingIndicatorControl { Dock = DockStyle.Fill };

        table.Controls.Add(Wrap("Artificial Horizon", _artificialHorizon), 0, 0);
        table.Controls.Add(Wrap("Heading Indicator", _headingIndicator), 1, 0);
        Controls.Add(table);
    }

    /// <summary>Drives the instruments from the fused attitude.</summary>
    public void RenderFrame(ProcessedFrame? frame, bool useFiltered)
    {
        if (frame is null)
        {
            return;
        }

        Orientation orientation = useFiltered ? frame.FilteredOrientation : frame.RawOrientation;
        _artificialHorizon.RollDegrees = orientation.RollDegrees;
        _artificialHorizon.PitchDegrees = orientation.PitchDegrees;
        _headingIndicator.HeadingDegrees = orientation.YawDegrees;
    }

    private static GroupBox Wrap(string title, Control control)
    {
        var group = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(10) };
        group.Controls.Add(control);
        return group;
    }
}
