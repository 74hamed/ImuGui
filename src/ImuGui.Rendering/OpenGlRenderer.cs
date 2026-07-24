using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace ImuGui.Rendering;

/// <summary>
/// Concrete OpenGL 3.3 core-profile renderer. All GL.* calls live here;
/// the controls interact only via <see cref="IRenderer"/>.
/// </summary>
internal sealed class OpenGlRenderer : IRenderer
{
    // -------------------------------------------------------------------------
    // GLSL — lit geometry (cube faces)
    // -------------------------------------------------------------------------
    private const string LitVertexShader = """
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec3 aColor;
        layout(location = 2) in vec3 aNormal;

        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;

        out vec3 vColor;
        out vec3 vNormal;     // world-space normal
        out vec3 vWorldPos;

        void main()
        {
            vec4 worldPos = uModel * vec4(aPosition, 1.0);
            vWorldPos  = worldPos.xyz;
            // Normal matrix = transpose(inverse(uModel)) — for orthogonal rotation
            // matrices (no non-uniform scale) this simplifies to the rotation itself.
            vNormal    = mat3(uModel) * aNormal;
            vColor     = aColor;
            gl_Position = uProjection * uView * worldPos;
        }
        """;

    private const string LitFragmentShader = """
        #version 330 core
        in vec3 vColor;
        in vec3 vNormal;
        in vec3 vWorldPos;

        uniform vec3 uLightDir;    // world-space, normalised, toward light
        uniform vec3 uLightColor;
        uniform float uAmbient;

        out vec4 FragColor;

        void main()
        {
            vec3 n  = normalize(vNormal);
            float d = max(dot(n, uLightDir), 0.0);
            vec3 lit = vColor * (uAmbient + (1.0 - uAmbient) * d * uLightColor);
            FragColor = vec4(lit, 1.0);
        }
        """;

    // -------------------------------------------------------------------------
    // GLSL — unlit lines (grid and axes)
    // -------------------------------------------------------------------------
    private const string LineVertexShader = """
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec3 aColor;

        uniform mat4 uView;
        uniform mat4 uProjection;

        out vec3 vColor;

        void main()
        {
            vColor      = aColor;
            gl_Position = uProjection * uView * vec4(aPosition, 1.0);
        }
        """;

    private const string LineFragmentShader = """
        #version 330 core
        in vec3 vColor;
        out vec4 FragColor;

        void main()
        {
            FragColor = vec4(vColor, 1.0);
        }
        """;

    // -------------------------------------------------------------------------
    // Scene constants
    // -------------------------------------------------------------------------
    private const float GridHalfExtent = 10f;          // ±10 → 20×20 cells
    private const int GridCellCount = 20;              // cells per side
    private const float AxisLength = 1.5f;             // axis arrow length
    private const float AmbientStrength = 0.35f;
    private static readonly Vector3 LightDirection = Vector3.Normalize(new Vector3(1f, 2f, 1.5f));
    private static readonly Vector3 LightColor = Vector3.One;  // white

    // Cube face colours
    private static readonly Vector3 ColorPosX = new(0.863f, 0.078f, 0.235f); // crimson
    private static readonly Vector3 ColorNegX = new(0.412f, 0.024f, 0.098f); // dark red
    private static readonly Vector3 ColorPosY = new(0.196f, 0.804f, 0.196f); // lime green
    private static readonly Vector3 ColorNegY = new(0.051f, 0.392f, 0.051f); // dark green
    private static readonly Vector3 ColorPosZ = new(0.255f, 0.412f, 0.882f); // royal blue
    private static readonly Vector3 ColorNegZ = new(0.090f, 0.168f, 0.510f); // dark blue

    // Grid colours
    private static readonly Vector3 GridMinorColor = new(0.25f, 0.25f, 0.25f);
    private static readonly Vector3 GridCenterColor = new(0.55f, 0.55f, 0.55f);

    // Axis colours (GL frame: X red, Y green, Z blue)
    private static readonly Vector3 AxisColorX = new(0.9f, 0.15f, 0.15f);
    private static readonly Vector3 AxisColorY = new(0.15f, 0.9f, 0.15f);
    private static readonly Vector3 AxisColorZ = new(0.15f, 0.15f, 0.9f);

    // -------------------------------------------------------------------------
    // GL handles
    // -------------------------------------------------------------------------
    private int _litProgram;
    private int _lineProgram;

    // Cube
    private int _cubeVao;
    private int _cubeVbo;
    private int _cubeVertexCount;

    // Grid
    private int _gridVao;
    private int _gridVbo;
    private int _gridVertexCount;

    // Axes
    private int _axesVao;
    private int _axesVbo;
    private int _axesVertexCount;

    // Lit program uniform locations
    private int _locLitModel;
    private int _locLitView;
    private int _locLitProjection;
    private int _locLitLightDir;
    private int _locLitLightColor;
    private int _locLitAmbient;

    // Line program uniform locations
    private int _locLineView;
    private int _locLineProjection;

    // Current view/projection (updated per frame via SetViewProjection)
    private Matrix4 _view = Matrix4.Identity;
    private Matrix4 _projection = Matrix4.Identity;

    private bool _disposed;

    // -------------------------------------------------------------------------
    // IRenderer
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public void Initialize()
    {
        _litProgram = BuildProgram(LitVertexShader, LitFragmentShader);
        _lineProgram = BuildProgram(LineVertexShader, LineFragmentShader);

        CacheUniformLocations();
        BuildCubeMesh();
        BuildGridMesh();
        BuildAxesMesh();

        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(TriangleFace.Back);
        GL.FrontFace(FrontFaceDirection.Ccw);
    }

    /// <inheritdoc/>
    public void Resize(int width, int height)
    {
        int safeHeight = Math.Max(height, 1);
        GL.Viewport(0, 0, width, safeHeight);
    }

    /// <inheritdoc/>
    public void BeginFrame(float r, float g, float b, float a = 1f)
    {
        GL.ClearColor(r, g, b, a);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }

    /// <inheritdoc/>
    public void SetViewProjection(Matrix4 view, Matrix4 projection)
    {
        _view = view;
        _projection = projection;
    }

    /// <inheritdoc/>
    public void DrawCube(Matrix4 model)
    {
        GL.Enable(EnableCap.CullFace);
        GL.UseProgram(_litProgram);
        GL.UniformMatrix4(_locLitModel, false, ref model);
        GL.UniformMatrix4(_locLitView, false, ref _view);
        GL.UniformMatrix4(_locLitProjection, false, ref _projection);
        GL.Uniform3(_locLitLightDir, LightDirection);
        GL.Uniform3(_locLitLightColor, LightColor);
        GL.Uniform1(_locLitAmbient, AmbientStrength);
        GL.BindVertexArray(_cubeVao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, _cubeVertexCount);
        GL.BindVertexArray(0);
    }

    /// <inheritdoc/>
    public void DrawGrid()
    {
        GL.Disable(EnableCap.CullFace);
        GL.UseProgram(_lineProgram);
        GL.UniformMatrix4(_locLineView, false, ref _view);
        GL.UniformMatrix4(_locLineProjection, false, ref _projection);
        GL.BindVertexArray(_gridVao);
        GL.DrawArrays(PrimitiveType.Lines, 0, _gridVertexCount);
        GL.BindVertexArray(0);
        GL.Enable(EnableCap.CullFace);
    }

    /// <inheritdoc/>
    public void DrawAxes()
    {
        GL.Disable(EnableCap.CullFace);
        GL.UseProgram(_lineProgram);
        GL.UniformMatrix4(_locLineView, false, ref _view);
        GL.UniformMatrix4(_locLineProjection, false, ref _projection);
        GL.BindVertexArray(_axesVao);
        GL.DrawArrays(PrimitiveType.Lines, 0, _axesVertexCount);
        GL.BindVertexArray(0);
        GL.Enable(EnableCap.CullFace);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            GL.DeleteVertexArrays(1, ref _cubeVao);
            GL.DeleteBuffers(1, ref _cubeVbo);
            GL.DeleteVertexArrays(1, ref _gridVao);
            GL.DeleteBuffers(1, ref _gridVbo);
            GL.DeleteVertexArrays(1, ref _axesVao);
            GL.DeleteBuffers(1, ref _axesVbo);
            GL.DeleteProgram(_litProgram);
            GL.DeleteProgram(_lineProgram);
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or DllNotFoundException
            or EntryPointNotFoundException)
        {
            // Teardown-only tolerance: the GL context may already be gone during shutdown,
            // in which case the driver reclaims these resources anyway. Traced, never silent.
            System.Diagnostics.Debug.WriteLine($"GL teardown skipped: {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // Shader compilation helpers
    // -------------------------------------------------------------------------

    private static int BuildProgram(string vertSrc, string fragSrc)
    {
        int vert = CompileShader(ShaderType.VertexShader, vertSrc);
        int frag = CompileShader(ShaderType.FragmentShader, fragSrc);
        int program = GL.CreateProgram();
        GL.AttachShader(program, vert);
        GL.AttachShader(program, frag);
        GL.LinkProgram(program);
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int linked);
        if (linked == 0)
        {
            string log = GL.GetProgramInfoLog(program);
            throw new InvalidOperationException($"GL program link failed: {log}");
        }

        GL.DeleteShader(vert);
        GL.DeleteShader(frag);
        return program;
    }

    private static int CompileShader(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int compiled);
        if (compiled == 0)
        {
            string log = GL.GetShaderInfoLog(shader);
            throw new InvalidOperationException($"GL shader compile failed ({type}): {log}");
        }

        return shader;
    }

    private void CacheUniformLocations()
    {
        _locLitModel = GL.GetUniformLocation(_litProgram, "uModel");
        _locLitView = GL.GetUniformLocation(_litProgram, "uView");
        _locLitProjection = GL.GetUniformLocation(_litProgram, "uProjection");
        _locLitLightDir = GL.GetUniformLocation(_litProgram, "uLightDir");
        _locLitLightColor = GL.GetUniformLocation(_litProgram, "uLightColor");
        _locLitAmbient = GL.GetUniformLocation(_litProgram, "uAmbient");
        _locLineView = GL.GetUniformLocation(_lineProgram, "uView");
        _locLineProjection = GL.GetUniformLocation(_lineProgram, "uProjection");
    }

    // -------------------------------------------------------------------------
    // Mesh builders
    // -------------------------------------------------------------------------

    // Vertex layout for lit mesh: position(3) + color(3) + normal(3) = 9 floats
    private const int LitStride = 9 * sizeof(float);
    // Vertex layout for line mesh: position(3) + color(3) = 6 floats
    private const int LineStride = 6 * sizeof(float);

    private void BuildCubeMesh()
    {
        // Each face: 2 triangles (6 vertices), interleaved position/color/normal.
        // Half-extent = 0.5; cube centred at origin.
        float[] verts = BuildCubeVertices();
        _cubeVertexCount = verts.Length / 9;

        GL.GenVertexArrays(1, out _cubeVao);
        GL.GenBuffers(1, out _cubeVbo);
        GL.BindVertexArray(_cubeVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _cubeVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * sizeof(float), verts, BufferUsageHint.StaticDraw);
        ConfigureLitAttributes();
        GL.BindVertexArray(0);
    }

    private static float[] BuildCubeVertices()
    {
        // Six faces, each defined by 4 corners in CCW winding when viewed from OUTSIDE
        // (i.e., from the direction of the face normal).
        // Verify with cross product: (B-A)×(C-A) must point outward for first triangle (A,B,C).
        // Cube centred at origin, half-extent 0.5.

        const float H = 0.5f;
        var faces = new (Vector3 n, Vector3 col, Vector3[] corners)[]
        {
            // +X face (crimson): viewed from +X, right=+Z, up=+Y → CCW: BL,BR,TR,TL
            (new Vector3(1,0,0), ColorPosX, [
                new Vector3(H,-H,-H), new Vector3(H, H,-H),
                new Vector3(H, H, H), new Vector3(H,-H, H)]),
            // -X face (dark red): viewed from -X, right=−Z, up=+Y → CCW
            (new Vector3(-1,0,0), ColorNegX, [
                new Vector3(-H,-H, H), new Vector3(-H, H, H),
                new Vector3(-H, H,-H), new Vector3(-H,-H,-H)]),
            // +Y face (lime green): viewed from +Y (top), right=+X, forward=+Z → CCW
            (new Vector3(0,1,0), ColorPosY, [
                new Vector3(-H, H, H), new Vector3( H, H, H),
                new Vector3( H, H,-H), new Vector3(-H, H,-H)]),
            // -Y face (dark green): viewed from -Y (bottom), right=+X, forward=-Z → CCW
            (new Vector3(0,-1,0), ColorNegY, [
                new Vector3(-H,-H,-H), new Vector3( H,-H,-H),
                new Vector3( H,-H, H), new Vector3(-H,-H, H)]),
            // +Z face (royal blue): viewed from +Z (front), right=−X, up=+Y → CCW
            (new Vector3(0,0,1), ColorPosZ, [
                new Vector3( H,-H, H), new Vector3( H, H, H),
                new Vector3(-H, H, H), new Vector3(-H,-H, H)]),
            // -Z face (dark blue): viewed from -Z (back), right=+X, up=+Y → CCW
            (new Vector3(0,0,-1), ColorNegZ, [
                new Vector3(-H,-H,-H), new Vector3(-H, H,-H),
                new Vector3( H, H,-H), new Vector3( H,-H,-H)]),
        };

        // 6 faces × 6 vertices × 9 floats
        float[] data = new float[6 * 6 * 9];
        int idx = 0;

        foreach (var (n, col, corners) in faces)
        {
            // Two triangles from quad corners 0,1,2,3 → (0,1,2) and (0,2,3)
            int[] tri = [0, 1, 2, 0, 2, 3];
            foreach (int ci in tri)
            {
                Vector3 p = corners[ci];
                data[idx++] = p.X;
                data[idx++] = p.Y;
                data[idx++] = p.Z;
                data[idx++] = col.X;
                data[idx++] = col.Y;
                data[idx++] = col.Z;
                data[idx++] = n.X;
                data[idx++] = n.Y;
                data[idx++] = n.Z;
            }
        }

        return data;
    }

    private static void ConfigureLitAttributes()
    {
        // position: location 0, offset 0
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, LitStride, 0);
        // color: location 1, offset 12
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, LitStride, 3 * sizeof(float));
        // normal: location 2, offset 24
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, LitStride, 6 * sizeof(float));
    }

    private void BuildGridMesh()
    {
        // Lines parallel to X and Z axes at 1-unit intervals over [-10, +10].
        // Centre lines (x=0 and z=0) use the brighter color.
        var verts = new List<float>();
        int halfCells = GridCellCount / 2;
        float spacing = (GridHalfExtent * 2f) / GridCellCount; // = 1.0

        // Lines parallel to Z (varying X)
        for (int i = -halfCells; i <= halfCells; i++)
        {
            float x = i * spacing;
            Vector3 color = i == 0 ? GridCenterColor : GridMinorColor;
            AppendLineVertex(verts, new Vector3(x, 0f, -GridHalfExtent), color);
            AppendLineVertex(verts, new Vector3(x, 0f, GridHalfExtent), color);
        }

        // Lines parallel to X (varying Z)
        for (int i = -halfCells; i <= halfCells; i++)
        {
            float z = i * spacing;
            Vector3 color = i == 0 ? GridCenterColor : GridMinorColor;
            AppendLineVertex(verts, new Vector3(-GridHalfExtent, 0f, z), color);
            AppendLineVertex(verts, new Vector3(GridHalfExtent, 0f, z), color);
        }

        float[] data = [.. verts];
        _gridVertexCount = data.Length / 6;

        GL.GenVertexArrays(1, out _gridVao);
        GL.GenBuffers(1, out _gridVbo);
        GL.BindVertexArray(_gridVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _gridVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, data.Length * sizeof(float), data, BufferUsageHint.StaticDraw);
        ConfigureLineAttributes();
        GL.BindVertexArray(0);
    }

    private void BuildAxesMesh()
    {
        // Three lines from origin along +X (red), +Y (green), +Z (blue).
        var verts = new List<float>();
        AppendLineVertex(verts, Vector3.Zero, AxisColorX);
        AppendLineVertex(verts, new Vector3(AxisLength, 0f, 0f), AxisColorX);
        AppendLineVertex(verts, Vector3.Zero, AxisColorY);
        AppendLineVertex(verts, new Vector3(0f, AxisLength, 0f), AxisColorY);
        AppendLineVertex(verts, Vector3.Zero, AxisColorZ);
        AppendLineVertex(verts, new Vector3(0f, 0f, AxisLength), AxisColorZ);

        float[] data = [.. verts];
        _axesVertexCount = data.Length / 6;

        GL.GenVertexArrays(1, out _axesVao);
        GL.GenBuffers(1, out _axesVbo);
        GL.BindVertexArray(_axesVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _axesVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, data.Length * sizeof(float), data, BufferUsageHint.StaticDraw);
        ConfigureLineAttributes();
        GL.BindVertexArray(0);
    }

    private static void AppendLineVertex(List<float> verts, Vector3 pos, Vector3 color)
    {
        verts.Add(pos.X);
        verts.Add(pos.Y);
        verts.Add(pos.Z);
        verts.Add(color.X);
        verts.Add(color.Y);
        verts.Add(color.Z);
    }

    private static void ConfigureLineAttributes()
    {
        // position: location 0, offset 0
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, LineStride, 0);
        // color: location 1, offset 12
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, LineStride, 3 * sizeof(float));
    }
}
