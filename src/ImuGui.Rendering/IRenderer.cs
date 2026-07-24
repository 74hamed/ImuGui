using OpenTK.Mathematics;

namespace ImuGui.Rendering;

/// <summary>
/// Seam between the WinForms controls and the OpenGL back-end.
/// All raw GL calls live in the implementing class; controls never call GL directly.
/// The only OpenTK type intentionally exposed here is <see cref="Matrix4"/>, which
/// is the math currency of the rendering pipeline.
/// </summary>
public interface IRenderer : IDisposable
{
    /// <summary>
    /// Initialises all GL resources (shaders, VAOs, VBOs).
    /// Must be called while the GL context is current.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Updates the viewport for the new client area size.
    /// Must be called while the GL context is current.
    /// </summary>
    /// <param name="width">New viewport width in pixels.</param>
    /// <param name="height">New viewport height in pixels (guarded against zero).</param>
    void Resize(int width, int height);

    /// <summary>
    /// Clears the back buffer to the given colour and prepares for a new frame.
    /// Must be called while the GL context is current.
    /// </summary>
    /// <param name="r">Red channel [0,1].</param>
    /// <param name="g">Green channel [0,1].</param>
    /// <param name="b">Blue channel [0,1].</param>
    /// <param name="a">Alpha channel [0,1].</param>
    void BeginFrame(float r, float g, float b, float a = 1f);

    /// <summary>
    /// Sets the view and projection matrices used by subsequent draw calls this frame.
    /// </summary>
    /// <param name="view">The camera view matrix.</param>
    /// <param name="projection">The projection matrix.</param>
    void SetViewProjection(Matrix4 view, Matrix4 projection);

    /// <summary>
    /// Draws the sensor cube with the given model matrix (includes attitude rotation,
    /// scale, and translation).
    /// </summary>
    /// <param name="model">The model-to-world transform.</param>
    void DrawCube(Matrix4 model);

    /// <summary>
    /// Draws the ground-reference grid centred at the world origin (y = 0 plane).
    /// </summary>
    void DrawGrid();

    /// <summary>
    /// Draws the world-space XYZ axes at the origin
    /// (X red, Y green, Z blue — GL right-handed Y-up frame).
    /// </summary>
    void DrawAxes();
}
