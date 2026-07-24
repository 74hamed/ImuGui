using System.ComponentModel;
using OpenTK.GLControl;
using OpenTK.Windowing.Common;

namespace ImuGui.Rendering;

/// <summary>
/// Abstract base for the two GL-backed UserControls.
/// Owns the <see cref="GLControl"/> and <see cref="IRenderer"/> lifecycle:
/// lazy initialisation on first paint, MakeCurrent before every GL call,
/// SwapBuffers at the end of paint, and safe teardown in Dispose.
/// Design-time mode is detected and all GL work is skipped, so the WinForms
/// designer renders a flat dark rectangle instead of crashing.
/// </summary>
/// <remarks>
/// Mouse and keyboard events from the inner <see cref="GLControl"/> child are
/// forwarded to this outer control's On* overrides so that derived classes can
/// handle them in one place (subclasses override <c>OnMouseDown</c>, etc., on this
/// class rather than subscribing to the inner control's events).
/// </remarks>
public abstract class GlSceneControl : UserControl
{
    private static readonly Color DesignTimeBackColor = Color.FromArgb(0x1A, 0x1A, 0x1A);
    private const float BackR = 0.075f;
    private const float BackG = 0.075f;
    private const float BackB = 0.085f;

    private GLControl? _glControl;
    private IRenderer? _renderer;
    private bool _glInitialised;
    private string? _glFailureMessage;
    private bool _disposed;

    /// <summary>Initialises the control and adds the child GLControl.</summary>
    protected GlSceneControl()
    {
        if (IsInDesignMode())
        {
            BackColor = DesignTimeBackColor;
            return;
        }

        _glControl = new GLControl(new GLControlSettings
        {
            API = ContextAPI.OpenGL,
            APIVersion = new Version(3, 3),
            Profile = ContextProfile.Core,
            DepthBits = 24,
            StencilBits = 0,
        });

        _glControl.Dock = DockStyle.Fill;
        _glControl.Paint += OnGlPaint;
        _glControl.Resize += OnGlResize;

        // Forward all interactive events from the inner GLControl to this control's
        // virtual On* methods so that subclasses override them in a single place.
        _glControl.MouseDown += (s, e) => OnMouseDown(e);
        _glControl.MouseUp += (s, e) => OnMouseUp(e);
        _glControl.MouseMove += (s, e) => OnMouseMove(e);
        _glControl.MouseWheel += (s, e) => OnMouseWheel(e);
        _glControl.KeyDown += (s, e) => OnKeyDown(e);
        _glControl.KeyUp += (s, e) => OnKeyUp(e);

        Controls.Add(_glControl);
    }

    // -------------------------------------------------------------------------
    // Abstract hooks for subclasses
    // -------------------------------------------------------------------------

    /// <summary>Called once after GL resources are ready. Subclasses set up their camera/state.</summary>
    protected abstract void OnGlInitialised();

    /// <summary>
    /// Renders one frame. The renderer's BeginFrame has already been called;
    /// subclasses set view/projection and issue draw calls.
    /// </summary>
    /// <param name="renderer">The initialised renderer.</param>
    protected abstract void OnRenderFrame(IRenderer renderer);

    // -------------------------------------------------------------------------
    // Protected helpers
    // -------------------------------------------------------------------------

    /// <summary>True when running inside the Visual Studio WinForms designer.</summary>
    protected static bool IsInDesignMode() =>
        LicenseManager.UsageMode == LicenseUsageMode.Designtime;

    /// <summary>
    /// Requests a repaint. Safe to call from the UI thread at any time;
    /// no-ops in design mode.
    /// </summary>
    protected void RequestRedraw()
    {
        if (!IsInDesignMode())
        {
            Invalidate();
            _glControl?.Invalidate();
        }
    }

    /// <summary>
    /// Moves keyboard focus to the inner GL surface so that key events are delivered
    /// via this control's key-down and key-up overrides.
    /// Call this from an OnMouseDown override when the control needs keyboard input.
    /// No-ops in design mode or before GL initialisation.
    /// </summary>
    protected void FocusGlSurface()
    {
        _glControl?.Focus();
    }

    // -------------------------------------------------------------------------
    // GLControl event handlers
    // -------------------------------------------------------------------------

    private void OnGlPaint(object? sender, PaintEventArgs e)
    {
        if (_glControl is null || _glFailureMessage is not null)
        {
            return;
        }

        try
        {
            _glControl.MakeCurrent();

            if (!_glInitialised)
            {
                _renderer = new OpenGlRenderer();
                _renderer.Initialize();
                _renderer.Resize(_glControl.Width, Math.Max(_glControl.Height, 1));
                _glInitialised = true;
                OnGlInitialised();
            }

            _renderer!.BeginFrame(BackR, BackG, BackB);
            OnRenderFrame(_renderer);
            _glControl.SwapBuffers();
        }
        catch (Exception ex)
        {
            // Containment boundary: throwing out of a Paint handler produces one error
            // dialog per repaint. Fail exactly once, keep the reason visible in the view
            // itself, and stop touching GL. The message is also traced for the log.
            EnterFailedState($"3D view unavailable:\n{ex.Message}");
        }
    }

    private void OnGlResize(object? sender, EventArgs e)
    {
        if (_glControl is null || !_glInitialised || _renderer is null || _glFailureMessage is not null)
        {
            return;
        }

        try
        {
            _glControl.MakeCurrent();
            _renderer.Resize(_glControl.Width, Math.Max(_glControl.Height, 1));
        }
        catch (Exception ex)
        {
            EnterFailedState($"3D view unavailable:\n{ex.Message}");
        }
    }

    private void EnterFailedState(string message)
    {
        _glFailureMessage = message;
        System.Diagnostics.Debug.WriteLine($"{GetType().Name}: {message}");

        // Hide the GL surface so this control's own OnPaint shows the diagnostic.
        if (_glControl is not null)
        {
            _glControl.Paint -= OnGlPaint;
            _glControl.Resize -= OnGlResize;
            _glControl.Visible = false;
        }

        Invalidate();
    }

    // -------------------------------------------------------------------------
    // Dispose
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            base.Dispose(disposing);
            return;
        }

        _disposed = true;

        if (disposing && _glControl is not null && _glInitialised && _renderer is not null)
        {
            try
            {
                _glControl.MakeCurrent();
                _renderer.Dispose();
            }
            catch (Exception ex) when (ex is InvalidOperationException
                or ObjectDisposedException
                or DllNotFoundException)
            {
                // Teardown-only tolerance: the native context can already be destroyed
                // during shutdown; the driver reclaims its resources. Traced, never silent.
                System.Diagnostics.Debug.WriteLine($"GL teardown skipped: {ex.Message}");
            }
        }

        base.Dispose(disposing);
    }

    // -------------------------------------------------------------------------
    // Design-time paint override
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        if (IsInDesignMode())
        {
            e.Graphics.Clear(DesignTimeBackColor);
            return;
        }

        if (_glFailureMessage is not null)
        {
            e.Graphics.Clear(DesignTimeBackColor);
            TextRenderer.DrawText(
                e.Graphics,
                _glFailureMessage + "\n\nAn OpenGL 3.3 capable GPU/driver is required for this view.",
                Font,
                ClientRectangle,
                Color.Gainsboro,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }

        base.OnPaint(e);
    }
}
