using System.Text.Json;
using ImuGui.Core.Calibration;
using Microsoft.Extensions.Logging;

namespace ImuGui.App.Settings;

/// <summary>Persists the calibration profile as %AppData%\ImuGui\calibration.json.</summary>
public sealed class JsonCalibrationProfileStore : ICalibrationProfileStore
{
    private readonly string _profileFilePath;
    private readonly ILogger<JsonCalibrationProfileStore> _logger;

    /// <summary>Creates the store.</summary>
    /// <param name="storageDirectory">The directory holding calibration.json.</param>
    /// <param name="logger">The logger.</param>
    public JsonCalibrationProfileStore(string storageDirectory, ILogger<JsonCalibrationProfileStore> logger)
    {
        _logger = logger;
        Directory.CreateDirectory(storageDirectory);
        _profileFilePath = Path.Combine(storageDirectory, "calibration.json");
    }

    /// <inheritdoc />
    public CalibrationProfile? Load()
    {
        if (!File.Exists(_profileFilePath))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(_profileFilePath);
            return JsonSerializer.Deserialize<CalibrationProfile>(json, JsonSettingsService.SerializerOptions);
        }
        catch (JsonException ex)
        {
            // Normalize parse failures to FormatException per the store contract; the
            // calibration service logs and falls back to the identity profile.
            _logger.LogWarning(ex, "Calibration profile at {Path} is unreadable.", _profileFilePath);
            throw new FormatException($"Calibration profile at '{_profileFilePath}' is not valid JSON.", ex);
        }
    }

    /// <inheritdoc />
    public void Save(CalibrationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        string json = JsonSerializer.Serialize(profile, JsonSettingsService.SerializerOptions);
        string tempPath = _profileFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _profileFilePath, overwrite: true);
        _logger.LogInformation("Calibration profile saved to {Path}.", _profileFilePath);
    }
}
