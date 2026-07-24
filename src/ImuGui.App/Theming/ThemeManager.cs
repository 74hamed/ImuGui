using ImuGui.Instruments;
using ScottPlot.WinForms;

namespace ImuGui.App.Theming;

/// <summary>
/// Applies the active <see cref="AppTheme"/> across a control tree. Charts and GL views
/// keep their own rendering (charts are restyled by <see cref="Views.ChartsPanel"/>; the
/// 3-D viewports stay dark by design, as in most 3-D tooling).
/// </summary>
public static class ThemeManager
{
    /// <summary>Tag marking a button styled with the accent color (primary action).</summary>
    public const string AccentButtonTag = "theme:accent-button";

    /// <summary>Tag marking a label rendered in the secondary text color.</summary>
    public const string SecondaryTextTag = "theme:secondary-text";

    /// <summary>Tag marking a thin panel used as a hairline separator.</summary>
    public const string SeparatorTag = "theme:separator";

    /// <summary>The active theme; <see cref="AppTheme.Dark"/> until changed.</summary>
    public static AppTheme Current { get; private set; } = AppTheme.Dark;

    /// <summary>Raised after <see cref="SetTheme"/> switches the active theme.</summary>
    public static event EventHandler? ThemeChanged;

    /// <summary>Switches the active theme (callers re-apply to their windows).</summary>
    public static void SetTheme(AppTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        if (ReferenceEquals(Current, theme))
        {
            return;
        }

        Current = theme;
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Applies the active theme to a whole window, including its title bar.</summary>
    public static void ApplyToWindow(Form form)
    {
        ArgumentNullException.ThrowIfNull(form);
        Apply(form);
        WindowChrome.TrySetDarkTitleBar(form, Current.IsDark);
    }

    /// <summary>Applies the active theme recursively to a control subtree.</summary>
    public static void Apply(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);
        ApplyRecursive(root, Current);
    }

    /// <summary>Styles one button as the accent-colored primary action.</summary>
    public static void StyleAccentButton(Button button)
    {
        AppTheme theme = Current;
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = theme.Accent;
        button.ForeColor = theme.AccentText;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(theme.Accent, 0.15f);
        button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(theme.Accent, 0.05f);
    }

    private static void ApplyRecursive(Control control, AppTheme theme)
    {
        switch (control)
        {
            case FormsPlot:
                return; // Charts are themed via the ScottPlot API, not WinForms colors.

            case Form form:
                form.BackColor = theme.WindowBackground;
                form.ForeColor = theme.PrimaryText;
                break;

            case Button button when Equals(button.Tag, AccentButtonTag):
                StyleAccentButton(button);
                break;

            case Button button:
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = theme.InputBackground;
                button.ForeColor = theme.PrimaryText;
                button.FlatAppearance.BorderColor = theme.Border;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(theme.InputBackground, 0.1f);
                button.FlatAppearance.MouseDownBackColor = theme.Border;
                break;

            case TextBox textBox:
                textBox.BackColor = theme.InputBackground;
                textBox.ForeColor = theme.PrimaryText;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                break;

            case NumericUpDown numericUpDown:
                numericUpDown.BackColor = theme.InputBackground;
                numericUpDown.ForeColor = theme.PrimaryText;
                break;

            case ComboBox comboBox:
                comboBox.BackColor = theme.InputBackground;
                comboBox.ForeColor = theme.PrimaryText;
                comboBox.FlatStyle = FlatStyle.Flat;
                break;

            case GroupBox groupBox:
                groupBox.BackColor = theme.SurfaceBackground;
                groupBox.ForeColor = theme.PrimaryText;
                break;

            case Label label:
                label.ForeColor = Equals(label.Tag, SecondaryTextTag) ? theme.SecondaryText : theme.PrimaryText;
                break;

            case CheckBox checkBox:
                checkBox.ForeColor = theme.PrimaryText;
                break;

            case RadioButton radioButton:
                radioButton.ForeColor = theme.PrimaryText;
                break;

            case StatusStrip statusStrip:
                statusStrip.BackColor = theme.SurfaceBackground;
                statusStrip.ForeColor = theme.PrimaryText;
                foreach (ToolStripItem item in statusStrip.Items)
                {
                    item.ForeColor = theme.PrimaryText;
                    item.BackColor = theme.SurfaceBackground;
                }

                break;

            case TabControl:
                break; // Pages are themed below; the header strip stays system-drawn.

            case TabPage tabPage:
                tabPage.BackColor = theme.WindowBackground;
                tabPage.ForeColor = theme.PrimaryText;
                break;

            case InstrumentControl instrument:
                instrument.BackColor = theme.SurfaceBackground;
                break;

            case Panel panel when Equals(panel.Tag, SeparatorTag):
                panel.BackColor = theme.Border;
                break;

            case TableLayoutPanel or FlowLayoutPanel or Panel:
                control.BackColor = Color.Transparent;
                control.ForeColor = theme.PrimaryText;
                break;

            case UserControl userControl:
                userControl.BackColor = theme.WindowBackground;
                userControl.ForeColor = theme.PrimaryText;
                break;

            default:
                control.BackColor = theme.WindowBackground;
                control.ForeColor = theme.PrimaryText;
                break;
        }

        foreach (Control child in control.Controls)
        {
            ApplyRecursive(child, theme);
        }
    }
}
