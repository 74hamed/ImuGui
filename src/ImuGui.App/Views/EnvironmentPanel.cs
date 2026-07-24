using ImuGui.App.Models;
using ImuGui.Core.Pipeline;
using ImuGui.Rendering;

namespace ImuGui.App.Views;

/// <summary>
/// The interactive 3-D environment: grid + axes + oriented sensor cube with the
/// mouse-driven camera. The control hints shown here match the implemented mapping
/// exactly (left-drag orbit, Ctrl+drag pan, wheel zoom, R reset).
/// </summary>
public sealed class EnvironmentPanel : UserControl
{
    private readonly EnvironmentGlView _environmentView;
    private readonly CheckBox _showGridCheckBox;

    public EnvironmentPanel()
    {
        Dock = DockStyle.Fill;

        _environmentView = new EnvironmentGlView { Dock = DockStyle.Fill };

        _showGridCheckBox = new CheckBox
        {
            Text = "Show grid",
            Checked = true,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
        };
        _showGridCheckBox.CheckedChanged += (_, _) =>
        {
            _environmentView.ShowGrid = _showGridCheckBox.Checked;
            GridVisibilityChanged?.Invoke(this, EventArgs.Empty);
        };

        var resetCameraButton = new Button { Text = "Reset camera", AutoSize = true };
        resetCameraButton.Click += (_, _) => _environmentView.ResetCamera();

        var controlHintsLabel = new Label
        {
            Text = "Middle-drag: orbit    Shift+Middle: pan    Ctrl+Middle: zoom    Wheel: zoom    Home: reset",
            AutoSize = true,
            Tag = Theming.ThemeManager.SecondaryTextTag,
            Anchor = AnchorStyles.Left,
            Padding = new Padding(12, 6, 0, 0),
        };

        var bottomBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8, 4, 8, 4),
        };
        bottomBar.Controls.Add(_showGridCheckBox);
        bottomBar.Controls.Add(resetCameraButton);
        bottomBar.Controls.Add(controlHintsLabel);

        Controls.Add(_environmentView);
        Controls.Add(bottomBar);
    }

    /// <summary>Raised when the user toggles the grid (for persistence).</summary>
    public event EventHandler? GridVisibilityChanged;

    /// <summary>Whether the reference grid is drawn.</summary>
    public bool ShowGrid
    {
        get => _showGridCheckBox.Checked;
        set => _showGridCheckBox.Checked = value;
    }

    /// <summary>Orients the environment's sensor cube from the fused attitude.</summary>
    public void RenderFrame(ProcessedFrame? frame, bool useFiltered)
    {
        if (frame is null || !Visible)
        {
            return;
        }

        _environmentView.SetAttitude(
            DisplayQuantityMapper.AttitudeFor(frame, DisplayQuantity.Orientation, useFiltered));
    }
}
