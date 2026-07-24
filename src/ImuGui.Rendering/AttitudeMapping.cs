using OpenTK.Mathematics;
using CoreQuaternion = ImuGui.Core.Models.Quaternion;

namespace ImuGui.Rendering;

/// <summary>
/// Converts Core quaternion attitudes (NED body-to-world) into OpenTK model-rotation
/// matrices (GL right-handed Y-up world frame).
/// </summary>
/// <remarks>
/// Derivation of the basis change:
/// <para>
/// NED world axes: X north, Y east, Z down.
/// GL world axes: X right, Y up, Z toward viewer.
/// The sensor cube uses NED body frame: X forward, Y right, Z down.
/// The GL cube uses: X forward (+X_gl), Y up (-Z_ned maps to +Y_gl), Z right (+Y_ned maps to +Z_gl).
/// </para>
/// <para>
/// Define the permutation matrix M that maps NED coordinates to GL coordinates:
///   x_gl = x_ned       (row 0: [1  0  0])
///   y_gl = -z_ned      (row 1: [0  0 -1])
///   z_gl = y_ned       (row 2: [0  1  0])
/// </para>
/// <para>
/// The NED rotation matrix R_ned (body→world in NED) comes directly from the quaternion.
/// In GL world coordinates the same physical rotation is:
///   R_gl = M · R_ned · Mᵀ
/// </para>
/// <para>
/// Acceptance check (identity quaternion → identity rotation):
///   R_ned = I  →  R_gl = M · I · Mᵀ = M · Mᵀ = I  ✓ (M is orthogonal).
/// </para>
/// </remarks>
internal static class AttitudeMapping
{
    // M: NED→GL permutation, stored as column vectors of M (row-major for clarity).
    // M = [ 1  0  0 ]
    //     [ 0  0 -1 ]
    //     [ 0  1  0 ]
    //
    // Mᵀ = [ 1  0  0 ]
    //      [ 0  0  1 ]
    //      [ 0 -1  0 ]

    /// <summary>
    /// Converts a NED body-to-world quaternion into a 4×4 GL model rotation matrix.
    /// The translation component is zero; callers add their own translation.
    /// </summary>
    /// <param name="attitude">
    /// Unit quaternion in NED convention (body X forward, Y right, Z down).
    /// </param>
    /// <returns>
    /// A rotation-only <see cref="Matrix4"/> in GL right-handed Y-up world space.
    /// </returns>
    internal static Matrix4 ToGlModelRotation(CoreQuaternion attitude)
    {
        // Extract the 3×3 rotation from the NED quaternion.
        // R_ned[col][row] in OpenTK column-major convention is built via
        // the standard quaternion-to-matrix formula:
        double w = attitude.W, x = attitude.X, y = attitude.Y, z = attitude.Z;

        // Precompute products (all doubled except diagonal).
        double xx = 2.0 * x * x, yy = 2.0 * y * y, zz = 2.0 * z * z;
        double xy = 2.0 * x * y, xz = 2.0 * x * z, yz = 2.0 * y * z;
        double wx = 2.0 * w * x, wy = 2.0 * w * y, wz = 2.0 * w * z;

        // R_ned (column-major, i.e. R[col, row]):
        //   col0 (body X in world NED): [ 1-yy-zz,  xy+wz,   xz-wy ]
        //   col1 (body Y in world NED): [ xy-wz,    1-xx-zz, yz+wx ]
        //   col2 (body Z in world NED): [ xz+wy,    yz-wx,   1-xx-yy ]
        // Stored as individual components r[row][col]:
        double r00 = 1 - yy - zz, r01 = xy - wz, r02 = xz + wy;
        double r10 = xy + wz, r11 = 1 - xx - zz, r12 = yz - wx;
        double r20 = xz - wy, r21 = yz + wx, r22 = 1 - xx - yy;

        // Apply R_gl = M · R_ned · Mᵀ
        // M  = [1,0,0; 0,0,-1; 0,1,0]
        // Mᵀ = [1,0,0; 0,0,1; 0,-1,0]
        //
        // Step 1: A = M · R_ned
        // A[i,j] = sum_k M[i,k]*R[k,j]
        // Row 0 of M is [1,0,0]: A[0,j] = R[0,j]
        // Row 1 of M is [0,0,-1]: A[1,j] = -R[2,j]
        // Row 2 of M is [0,1,0]:  A[2,j] = R[1,j]
        double a00 = r00, a01 = r01, a02 = r02;
        double a10 = -r20, a11 = -r21, a12 = -r22;
        double a20 = r10, a21 = r11, a22 = r12;

        // Step 2: R_gl = A · Mᵀ
        // Mᵀ = [ 1  0  0 ]    (transpose of M)
        //      [ 0  0  1 ]
        //      [ 0 -1  0 ]
        // R_gl[i,0] = A[i,0]*1 + A[i,1]*0 + A[i,2]*0 =  A[i,0]
        // R_gl[i,1] = A[i,0]*0 + A[i,1]*0 + A[i,2]*(-1) = -A[i,2]
        // R_gl[i,2] = A[i,0]*0 + A[i,1]*1 + A[i,2]*0 =  A[i,1]
        float g00 = (float)a00, g01 = (float)(-a02), g02 = (float)a01;
        float g10 = (float)a10, g11 = (float)(-a12), g12 = (float)a11;
        float g20 = (float)a20, g21 = (float)(-a22), g22 = (float)a21;

        // OpenTK Matrix4 is stored row-major, but GL reads it column-major.
        // When passed with transpose=false, GLSL "M * v" computes Transpose(OpenTK_M) * v.
        // Therefore we must store the TRANSPOSE of R_gl: OpenTK Row i = column i of R_gl.
        //   Row0 = column 0 of R_gl = (g00, g10, g20)
        //   Row1 = column 1 of R_gl = (g01, g11, g21)
        //   Row2 = column 2 of R_gl = (g02, g12, g22)
        return new Matrix4(
            new Vector4(g00, g10, g20, 0f),   // row 0 = col 0 of R_gl
            new Vector4(g01, g11, g21, 0f),   // row 1 = col 1 of R_gl
            new Vector4(g02, g12, g22, 0f),   // row 2 = col 2 of R_gl
            new Vector4(0f,  0f,  0f,  1f));  // row 3 (homogeneous)
    }
}
