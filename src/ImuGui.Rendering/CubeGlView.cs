using OpenTK.Mathematics;
using CoreQuaternion = ImuGui.Core.Models.Quaternion;

namespace ImuGui.Rendering;

/// <summary>
/// A self-contained 3D view that renders a coloured sensor cube on a dark background.
/// The cube is oriented by the most recently supplied attitude quaternion; the camera
/// is fixed at a slightly elevated position looking at the origin.
/// </summary>
/// <remarks>
/// <para>Thread safety: <see cref="SetAttitude"/> must be called on the UI thread.</para>
/// </remarks>
public sealed class CubeGlView : GlSceneControl
{
    // Fixed camera pose: the cube should fill most of the view.
    // Eye is positioned to the right-front and slightly above, looking at the origin.
    // Distance ≈ 2.9 units with 45° FOV → cube angular size ≈ 20° (≈ 44% of view width).
    private static readonly Vector3 FixedEye = new(1.8f, 1.3f, 1.8f);
    private static readonly Vector3 FixedTarget = Vector3.Zero;
    private static readonly Vector3 FixedUp = Vector3.UnitY;

    private const float FovRadians = MathF.PI / 4f;   // 45°
    private const float NearPlane = 0.1f;
    private const float FarPlane = 50f;

    private Matrix4 _view = Matrix4.Identity;
    private Matrix4 _projection = Matrix4.Identity;

    // Current attitude (NED body-to-world); identity by default.
    private CoreQuaternion _attitude = CoreQuaternion.Identity;

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

    /// <inheritdoc/>
    protected override void OnGlInitialised()
    {
        _view = Matrix4.LookAt(FixedEye, FixedTarget, FixedUp);
        UpdateProjection();
    }

    /// <inheritdoc/>
    protected override void OnRenderFrame(IRenderer renderer)
    {
        UpdateProjection();
        renderer.SetViewProjection(_view, _projection);

        Matrix4 model = AttitudeMapping.ToGlModelRotation(_attitude);
        renderer.DrawCube(model);
    }

    private void UpdateProjection()
    {
        int w = Math.Max(Width, 1);
        int h = Math.Max(Height, 1);
        float aspect = (float)w / h;
        _projection = Matrix4.CreatePerspectiveFieldOfView(FovRadians, aspect, NearPlane, FarPlane);
    }
}
