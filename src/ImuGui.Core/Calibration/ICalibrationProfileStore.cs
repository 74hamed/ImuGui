namespace ImuGui.Core.Calibration;

/// <summary>Persists the active calibration profile between runs (implemented by the app layer).</summary>
public interface ICalibrationProfileStore
{
    /// <summary>Loads the persisted profile, or null when none exists.</summary>
    CalibrationProfile? Load();

    /// <summary>Persists the profile.</summary>
    /// <param name="profile">The profile to save.</param>
    void Save(CalibrationProfile profile);
}
