using ImuGui.App.Models;
using ImuGui.App.Settings;
using ImuGui.Core.Pipeline;
using ImuGui.Rendering;

namespace ImuGui.App.Views;

/// <summary>
/// Two independent 3-D cube views, each driven by a user-selectable quantity with a
/// per-view raw/filtered toggle. Mutual exclusion of quantities is enforced by the shared
/// <see cref="CubeViewSelectionModel"/> (enum-based — no display-string matching).
/// </summary>
public sealed class CubeViewsPanel : UserControl
{
    private readonly CubeGlView _primaryCubeView;
    private readonly CubeGlView _secondaryCubeView;
    private readonly ComboBox _primaryQuantityComboBox;
    private readonly ComboBox _secondaryQuantityComboBox;
    private readonly CheckBox _primaryFilteredCheckBox;
    private readonly CheckBox _secondaryFilteredCheckBox;
    private CubeViewSelectionModel _selectionModel = new(
        DisplayQuantity.Accelerometer, DisplayQuantity.Orientation);
    private bool _updatingComboBoxes;

    public CubeViewsPanel()
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

        table.Controls.Add(
            BuildViewGroup(
                "View 1",
                out _primaryCubeView, out _primaryQuantityComboBox, out _primaryFilteredCheckBox),
            0, 0);
        table.Controls.Add(
            BuildViewGroup(
                "View 2",
                out _secondaryCubeView, out _secondaryQuantityComboBox, out _secondaryFilteredCheckBox),
            1, 0);

        _primaryQuantityComboBox.SelectedIndexChanged += (_, _) => OnQuantityPicked(isPrimary: true);
        _secondaryQuantityComboBox.SelectedIndexChanged += (_, _) => OnQuantityPicked(isPrimary: false);
        Controls.Add(table);
    }

    /// <summary>Applies persisted selections and wires the shared selection model.</summary>
    public void Initialize(UserSettings settings)
    {
        _selectionModel = new CubeViewSelectionModel(
            settings.PrimaryCubeQuantity, settings.SecondaryCubeQuantity);
        _selectionModel.SelectionsChanged += (_, _) => RefreshComboBoxes();
        _primaryFilteredCheckBox.Checked = settings.PrimaryCubeUsesFiltered;
        _secondaryFilteredCheckBox.Checked = settings.SecondaryCubeUsesFiltered;
        RefreshComboBoxes();
    }

    /// <summary>Updates both cubes from the frame (skipped while hidden).</summary>
    public void RenderTick(ProcessedFrame? frame)
    {
        if (frame is null || !Visible)
        {
            return;
        }

        _primaryCubeView.SetAttitude(DisplayQuantityMapper.AttitudeFor(
            frame, _selectionModel.Primary, _primaryFilteredCheckBox.Checked));
        _secondaryCubeView.SetAttitude(DisplayQuantityMapper.AttitudeFor(
            frame, _selectionModel.Secondary, _secondaryFilteredCheckBox.Checked));
    }

    /// <summary>Current selections, for persistence on exit.</summary>
    public (DisplayQuantity Primary, DisplayQuantity Secondary, bool PrimaryFiltered, bool SecondaryFiltered)
        SnapshotPreferences() => (
        _selectionModel.Primary,
        _selectionModel.Secondary,
        _primaryFilteredCheckBox.Checked,
        _secondaryFilteredCheckBox.Checked);

    private static GroupBox BuildViewGroup(
        string title, out CubeGlView cubeView, out ComboBox quantityComboBox, out CheckBox filteredCheckBox)
    {
        var group = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(8) };

        cubeView = new CubeGlView { Dock = DockStyle.Fill };

        quantityComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 160,
        };
        filteredCheckBox = new CheckBox { Text = "Filtered", Checked = true, AutoSize = true };

        var topBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        topBar.Controls.Add(quantityComboBox);
        topBar.Controls.Add(filteredCheckBox);

        group.Controls.Add(cubeView);
        group.Controls.Add(topBar);
        return group;
    }

    private void OnQuantityPicked(bool isPrimary)
    {
        if (_updatingComboBoxes)
        {
            return;
        }

        ComboBox comboBox = isPrimary ? _primaryQuantityComboBox : _secondaryQuantityComboBox;
        if (comboBox.SelectedItem is not DisplayQuantity picked)
        {
            return;
        }

        bool accepted = isPrimary
            ? _selectionModel.TrySetPrimary(picked)
            : _selectionModel.TrySetSecondary(picked);
        if (!accepted)
        {
            RefreshComboBoxes(); // Revert to a consistent state.
        }
    }

    private void RefreshComboBoxes()
    {
        _updatingComboBoxes = true;
        try
        {
            FillComboBox(_primaryQuantityComboBox, _selectionModel.AvailableForPrimary, _selectionModel.Primary);
            FillComboBox(
                _secondaryQuantityComboBox, _selectionModel.AvailableForSecondary, _selectionModel.Secondary);
        }
        finally
        {
            _updatingComboBoxes = false;
        }
    }

    private static void FillComboBox(
        ComboBox comboBox, IReadOnlyList<DisplayQuantity> available, DisplayQuantity selected)
    {
        comboBox.Items.Clear();
        foreach (DisplayQuantity quantity in available)
        {
            comboBox.Items.Add(quantity);
        }

        comboBox.SelectedItem = selected;
    }
}
