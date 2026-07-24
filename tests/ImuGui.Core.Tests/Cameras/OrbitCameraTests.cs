using FluentAssertions;
using ImuGui.Core.Cameras;
using ImuGui.Core.Fusion;
using ImuGui.Core.Models;
using Xunit;

namespace ImuGui.Core.Tests.Cameras;

public class OrbitCameraTests
{
    [Fact]
    public void Starts_with_an_initialized_rotation_center_and_sane_pose()
    {
        var camera = new OrbitCamera();

        camera.Target.Should().Be(Vector3.Zero, "the rotation center must never be uninitialized");
        camera.Distance.Should().Be(8);

        CameraPose pose = camera.Pose;
        (pose.EyePosition - pose.Target).Magnitude.Should().BeApproximately(8, 1e-9);
        pose.Up.Magnitude.Should().BeApproximately(1, 1e-9);
        pose.Up.Y.Should().BeGreaterThan(0, "the default pose is above the grid looking down");
    }

    [Fact]
    public void Orbit_changes_yaw_and_clamps_pitch()
    {
        var camera = new OrbitCamera();
        double initialYaw = camera.YawRadians;

        camera.Orbit(0.5, 0);
        camera.YawRadians.Should().BeApproximately(initialYaw + 0.5, 1e-12);

        camera.Orbit(0, 10);
        camera.PitchRadians.Should().BeApproximately(AngleMath.DegreesToRadians(89), 1e-9);

        camera.Orbit(0, -20);
        camera.PitchRadians.Should().BeApproximately(AngleMath.DegreesToRadians(-89), 1e-9);
    }

    [Fact]
    public void Zoom_is_multiplicative_and_clamped()
    {
        var camera = new OrbitCamera();

        camera.Zoom(0.5);
        camera.Distance.Should().Be(4);

        camera.Zoom(1e-9);
        camera.Distance.Should().Be(OrbitCamera.MinimumDistance);

        camera.Zoom(1e9);
        camera.Distance.Should().Be(OrbitCamera.MaximumDistance);

        Action act = () => camera.Zoom(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Pan_moves_the_rotation_center_along_camera_axes_and_is_reversible()
    {
        var camera = new OrbitCamera();

        camera.Pan(1.0, 0.5);
        camera.Target.Magnitude.Should().BeGreaterThan(0);

        camera.Pan(-1.0, -0.5);
        camera.Target.Magnitude.Should().BeLessThan(1e-9, "panning back restores the center");
    }

    [Fact]
    public void Reset_restores_the_documented_default_pose()
    {
        var camera = new OrbitCamera();
        camera.Orbit(1.2, -0.4);
        camera.Zoom(0.3);
        camera.Pan(3, 2);

        camera.Reset();

        camera.Target.Should().Be(Vector3.Zero);
        camera.Distance.Should().Be(8);
        camera.YawRadians.Should().BeApproximately(Math.PI / 4, 1e-12);
        camera.PitchRadians.Should().BeApproximately(Math.PI / 6, 1e-12);
    }

    [Fact]
    public void Pose_up_stays_orthogonal_to_the_view_direction()
    {
        var camera = new OrbitCamera();
        camera.Orbit(2.3, 0.9);
        camera.Pan(1.5, -0.7);

        CameraPose pose = camera.Pose;
        Vector3 forward = (pose.Target - pose.EyePosition).Normalized();

        forward.Dot(pose.Up).Should().BeApproximately(0, 1e-9);
    }
}
