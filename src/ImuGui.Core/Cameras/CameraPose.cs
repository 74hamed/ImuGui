using ImuGui.Core.Models;

namespace ImuGui.Core.Cameras;

/// <summary>An eye/target/up triple ready to feed a look-at view matrix.</summary>
/// <param name="EyePosition">The camera position.</param>
/// <param name="Target">The point the camera looks at (the orbit center).</param>
/// <param name="Up">The camera's up direction (unit length).</param>
public sealed record CameraPose(Vector3 EyePosition, Vector3 Target, Vector3 Up);
