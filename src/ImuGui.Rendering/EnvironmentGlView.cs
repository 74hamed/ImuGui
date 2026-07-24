using ImuGui.Core.Cameras;
using OpenTK.Mathematics;
using CoreQuaternion = ImuGui.Core.Models.Quaternion;

namespace ImuGui.Rendering;

/// <summary>
/// Interactive 3D environment view: a ground grid, world-axis indicators, and
/// a sensor cube that can be orbited with the mouse.
/// </summary>
/// <remarks>
/// <para>
/// Mouse controls follow Blender's viewport navigation (exactly as documented; the
/// modifier held when the middle button goes down selects the drag mode, as in Blender):
/// <list type="bullet">
///   <item>Middle-drag: orbit (0.01 rad/px; horizontal → yaw, vertical → pitch).</item>
///   <item>Shift + middle-drag: pan (target moves in camera plane; scale = Distance × 0.002 world-units/px).</item>
///   <item>Ctrl + middle-drag: zoom (drag up = closer, 1% per pixel).</item>
///   <item>Mouse wheel: zoom, factor 0.9 per 120 wheel-delta notches (wheel up = closer).</item>
///   <item>Home (or R): reset camera.</item>
/// </list>
/// </para>
/// <para>Thread safety: all public methods must be called on the UI thread.</para>
/// </remarks>
public sealed class EnvironmentGlView : GlSceneControl
{
    /// <summary>Initialises the view and configures it to receive keyboard input.</summary>
    public EnvironmentGlView()
    {
        // Must be Selectable so the control can receive focus and keyboard events
        // (the R key resets the camera). Set in constructor, before layout.
        SetStyle(ControlStyles.Selectable, true);
    }

    // Sensor cube floats above the grid.
    private const float CubeScale = 1.2f;
    private const float CubeElevation = 1.5f;

    private const float FovRadians = MathF.PI / 4f;    // 45°
    private const float NearPlane = 0.1f;
    private const float FarPlane = 200f;

    // Mouse orbit sensitivity
    private const float OrbitSensitivity = 0.01f;      // rad/px
    // Pan world-units per pixel = Distance × PanScale
    private const float PanScale = 0.002f;
    // Zoom factor per 120 wheel-delta notch (positive delta = wheel up = zoom in)
    private const float ZoomFactor = 0.9f;
    private const int WheelNotch = 120;
    // Ctrl+middle-drag zoom: 1% distance change per vertical pixel (drag up = closer)
    private const double ZoomDragFactorPerPixel = 1.01;

    private readonly OrbitCamera _camera = new();

    private Matrix4 _projection = Matrix4.Identity;
    private CoreQuaternion _attitude = CoreQuaternion.Identity;
    private bool _showGrid = true;

    // Mouse drag state (mode is chosen when the middle button goes down, as in Blender)
    private enum DragMode
    {
        None,
        Orbit,
        Pan,
        ZoomDrag,
    }

    private DragMode _dragMode = DragMode.None;
    private int _lastMouseX;
    private int _lastMouseY;

    /// <summary>
    /// Gets or sets whether the ground reference grid is rendered.
    /// Defaults to <see langword="true"/>. Changing this value invalidates the view.
    /// </summary>
    public bool ShowGrid
    {
        get => _showGrid;
        set
        {
            if (_showGrid == value)
            {
                return;
            }

            _showGrid = value;
            RequestRedraw();
        }
    }

    /// <summary>
    /// Stores the attitude quaternion and requests a repaint.
    /// Must be called on the UI thread.
    /// </summary>
    /// <param name="attitude">
    /// The body-to-world attitude in NED frame (body X forward, Y right, Z down).
    /// </param>
    public void SetAttitude(CoreQuaternion attitude)
    {
        _attitude = attitude;
        RequestRedraw();
    }

    /// <summary>
    /// Resets the orbit camera to its default pose (45° azimuth, 30° elevation, distance 8).
    /// </summary>
    public void ResetCamera()
    {
        _camera.Reset();
        RequestRedraw();
    }

    // -------------------------------------------------------------------------
    // GlSceneControl overrides
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void OnGlInitialised()
    {
        UpdateProjection();
    }

    /// <inheritdoc/>
    protected override void OnRenderFrame(IRenderer renderer)
    {
        UpdateProjection();

        CameraPose pose = _camera.Pose;
        var eye = new Vector3((float)pose.EyePosition.X, (float)pose.EyePosition.Y, (float)pose.EyePosition.Z);
        var target = new Vector3((float)pose.Target.X, (float)pose.Target.Y, (float)pose.Target.Z);
        var up = new Vector3((float)pose.Up.X, (float)pose.Up.Y, (float)pose.Up.Z);
        Matrix4 view = Matrix4.LookAt(eye, target, up);

        renderer.SetViewProjection(view, _projection);

        if (_showGrid)
        {
            renderer.DrawGrid();
        }

        renderer.DrawAxes();

        // Cube: scale first (uniform), then rotate by attitude, then translate (elevate).
        // OpenTK * is mathematical matrix product; GLSL model*v applies right to left.
        // To get: translate(rotate(scale(v))) = T * R * S (mathematical order).
        Matrix4 rotation = AttitudeMapping.ToGlModelRotation(_attitude);
        Matrix4 scale = Matrix4.CreateScale(CubeScale);
        Matrix4 translation = Matrix4.CreateTranslation(0f, CubeElevation, 0f);
        Matrix4 model = translation * rotation * scale;
        renderer.DrawCube(model);
    }

    // -------------------------------------------------------------------------
    // Mouse input
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (IsInDesignMode())
        {
            return;
        }

        FocusGlSurface();  // ensure keyboard events reach our OnKeyDown
        if (e.Button == MouseButtons.Middle)
        {
            // Blender picks the navigation mode from the modifiers held at press time.
            _dragMode = (ModifierKeys & Keys.Shift) != 0 ? DragMode.Pan
                : (ModifierKeys & Keys.Control) != 0 ? DragMode.ZoomDrag
                : DragMode.Orbit;
            _lastMouseX = e.X;
            _lastMouseY = e.Y;
        }
    }

    /// <inheritdoc/>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Middle)
        {
            _dragMode = DragMode.None;
        }
    }

    /// <inheritdoc/>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragMode == DragMode.None || IsInDesignMode())
        {
            return;
        }

        int dx = e.X - _lastMouseX;
        int dy = e.Y - _lastMouseY;
        _lastMouseX = e.X;
        _lastMouseY = e.Y;

        switch (_dragMode)
        {
            case DragMode.Pan:
                // Pan: the scene follows the mouse.
                // WinForms Y increases downward; a pixel drag of (dx, dy) means:
                //   dx > 0 → mouse right → scene right → deltaRight = -dx (camera slides left)
                //   dy < 0 → mouse up    → scene up    → deltaUp    = -dy (positive when mouse up)
                double panScale = _camera.Distance * PanScale;
                _camera.Pan(-dx * panScale, -dy * panScale);
                break;

            case DragMode.ZoomDrag:
                // Ctrl+middle vertical drag, as in Blender: up = closer, down = farther.
                if (dy != 0)
                {
                    _camera.Zoom(Math.Pow(ZoomDragFactorPerPixel, dy));
                }

                break;

            case DragMode.Orbit:
                // Orbit: dragging right increases yaw clockwise from above (positive delta),
                //        dragging up increases pitch (look from higher = positive pitch).
                _camera.Orbit(dx * OrbitSensitivity, -dy * OrbitSensitivity);
                break;

            case DragMode.None:
            default:
                break;
        }

        RequestRedraw();
    }

    /// <inheritdoc/>
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (IsInDesignMode())
        {
            return;
        }

        // Positive delta (wheel up) = zoom in = distance * ZoomFactor^notches.
        int notches = e.Delta / WheelNotch;
        if (notches == 0)
        {
            return;
        }

        double factor = Math.Pow(ZoomFactor, notches);
        _camera.Zoom(factor);
        RequestRedraw();
    }

    // -------------------------------------------------------------------------
    // Keyboard input
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsInDesignMode())
        {
            return;
        }

        if (e.KeyCode is Keys.Home or Keys.R)
        {
            // Home matches Blender's "frame view"; R is kept as a familiar alias.
            ResetCamera();
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void UpdateProjection()
    {
        int w = Math.Max(Width, 1);
        int h = Math.Max(Height, 1);
        float aspect = (float)w / h;
        _projection = Matrix4.CreatePerspectiveFieldOfView(FovRadians, aspect, NearPlane, FarPlane);
    }
}
