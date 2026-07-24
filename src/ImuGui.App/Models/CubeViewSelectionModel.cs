namespace ImuGui.App.Models;

/// <summary>
/// Shared selection state for the two cube views: each view picks one
/// <see cref="DisplayQuantity"/>, and a quantity selected in one view is removed from the
/// other's options (enum + shared state — never display-string matching).
/// </summary>
public sealed class CubeViewSelectionModel
{
    /// <summary>Creates the model, falling back to defaults when the selections collide.</summary>
    /// <param name="primary">The first view's initial quantity.</param>
    /// <param name="secondary">The second view's initial quantity.</param>
    public CubeViewSelectionModel(DisplayQuantity primary, DisplayQuantity secondary)
    {
        if (primary == secondary)
        {
            primary = DisplayQuantity.Accelerometer;
            secondary = DisplayQuantity.Orientation;
        }

        Primary = primary;
        Secondary = secondary;
    }

    /// <summary>Raised after either selection changes.</summary>
    public event EventHandler? SelectionsChanged;

    /// <summary>The first view's quantity.</summary>
    public DisplayQuantity Primary { get; private set; }

    /// <summary>The second view's quantity.</summary>
    public DisplayQuantity Secondary { get; private set; }

    /// <summary>Options currently available to the first view.</summary>
    public IReadOnlyList<DisplayQuantity> AvailableForPrimary => AllExcept(Secondary);

    /// <summary>Options currently available to the second view.</summary>
    public IReadOnlyList<DisplayQuantity> AvailableForSecondary => AllExcept(Primary);

    /// <summary>Selects the first view's quantity; rejected when the other view holds it.</summary>
    /// <param name="quantity">The requested quantity.</param>
    public bool TrySetPrimary(DisplayQuantity quantity)
    {
        if (quantity == Secondary)
        {
            return false;
        }

        if (Primary != quantity)
        {
            Primary = quantity;
            SelectionsChanged?.Invoke(this, EventArgs.Empty);
        }

        return true;
    }

    /// <summary>Selects the second view's quantity; rejected when the other view holds it.</summary>
    /// <param name="quantity">The requested quantity.</param>
    public bool TrySetSecondary(DisplayQuantity quantity)
    {
        if (quantity == Primary)
        {
            return false;
        }

        if (Secondary != quantity)
        {
            Secondary = quantity;
            SelectionsChanged?.Invoke(this, EventArgs.Empty);
        }

        return true;
    }

    private static DisplayQuantity[] AllExcept(DisplayQuantity taken) =>
        Enum.GetValues<DisplayQuantity>().Where(quantity => quantity != taken).ToArray();
}
