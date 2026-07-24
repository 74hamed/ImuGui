using ImuGui.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImuGui.Core.Calibration;

/// <summary>The default <see cref="ICalibrationService"/>.</summary>
public sealed class CalibrationService : ICalibrationService
{
    private readonly ICalibrationProfileStore? _store;
    private readonly ILogger _logger;
    private volatile CalibrationProfile _currentProfile = CalibrationProfile.Identity;

    /// <summary>Creates the service, loading any persisted profile.</summary>
    /// <param name="store">Optional persistence; when null, profiles live only in memory.</param>
    /// <param name="logger">Optional logger.</param>
    public CalibrationService(
        ICalibrationProfileStore? store = null, ILogger<CalibrationService>? logger = null)
    {
        _store = store;
        _logger = logger ?? NullLogger<CalibrationService>.Instance;

        if (_store is null)
        {
            return;
        }

        try
        {
            CalibrationProfile? persisted = _store.Load();
            if (persisted is not null)
            {
                _currentProfile = persisted;
                _logger.LogInformation(
                    "Loaded persisted calibration profile (created {CreatedUtc}).", persisted.CreatedUtc);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            _logger.LogWarning(ex, "Could not load the persisted calibration profile; using identity.");
        }
    }

    /// <inheritdoc />
    public event EventHandler? ProfileChanged;

    /// <inheritdoc />
    public CalibrationProfile CurrentProfile => _currentProfile;

    /// <inheritdoc />
    public void ApplyProfile(CalibrationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _currentProfile = profile;
        ProfileChanged?.Invoke(this, EventArgs.Empty);
        _logger.LogInformation("Calibration profile applied (identity: {IsIdentity}).", profile.IsIdentity);

        if (_store is null)
        {
            return;
        }

        try
        {
            _store.Save(profile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Calibration profile applied but could not be persisted.");
            throw new CalibrationException(
                $"The calibration profile is active but could not be saved for future runs: {ex.Message}",
                ex);
        }
    }

    /// <inheritdoc />
    public void ResetToIdentity() => ApplyProfile(CalibrationProfile.Identity);

    /// <inheritdoc />
    public SensorSample Correct(SensorSample sample) => _currentProfile.Apply(sample);
}
