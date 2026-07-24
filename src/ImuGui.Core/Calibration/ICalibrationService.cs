using ImuGui.Core.Models;

namespace ImuGui.Core.Calibration;

/// <summary>
/// Owns the active <see cref="CalibrationProfile"/>: applies it to live samples, swaps it
/// when a calibration routine completes, and persists it via <see cref="ICalibrationProfileStore"/>.
/// Thread-safe: <see cref="Correct"/> may run on the acquisition thread while the UI applies
/// a new profile.
/// </summary>
public interface ICalibrationService
{
    /// <summary>The active profile (<see cref="CalibrationProfile.Identity"/> when uncalibrated).</summary>
    CalibrationProfile CurrentProfile { get; }

    /// <summary>Raised after the active profile changes.</summary>
    event EventHandler? ProfileChanged;

    /// <summary>Makes the profile active and persists it.</summary>
    /// <param name="profile">The new profile.</param>
    /// <exception cref="CalibrationException">
    /// The profile was applied in memory but could not be persisted.
    /// </exception>
    void ApplyProfile(CalibrationProfile profile);

    /// <summary>Reverts to the identity profile and persists that choice.</summary>
    void ResetToIdentity();

    /// <summary>Applies the active profile to one sample.</summary>
    /// <param name="sample">The raw sample.</param>
    SensorSample Correct(SensorSample sample);
}
