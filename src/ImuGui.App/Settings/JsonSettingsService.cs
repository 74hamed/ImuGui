using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ImuGui.App.Settings;

/// <summary>
/// JSON-file settings persistence under %AppData%\ImuGui. Loads leniently (a corrupt file
/// is quarantined and defaults are used), saves atomically (temp file + move).
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _settingsFilePath;
    private readonly ILogger<JsonSettingsService> _logger;
    private readonly object _sync = new();

    /// <summary>Creates the service and loads persisted settings immediately.</summary>
    /// <param name="storageDirectory">The directory holding settings.json.</param>
    /// <param name="logger">The logger.</param>
    public JsonSettingsService(string storageDirectory, ILogger<JsonSettingsService> logger)
    {
        _logger = logger;
        Directory.CreateDirectory(storageDirectory);
        _settingsFilePath = Path.Combine(storageDirectory, "settings.json");
        Current = LoadOrDefaults();
    }

    /// <inheritdoc />
    public UserSettings Current { get; private set; }

    /// <inheritdoc />
    public void Update(Func<UserSettings, UserSettings> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        lock (_sync)
        {
            Current = mutate(Current);
        }
    }

    /// <inheritdoc />
    public void Save()
    {
        lock (_sync)
        {
            try
            {
                string json = JsonSerializer.Serialize(Current, SerializerOptions);
                string tempPath = _settingsFilePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _settingsFilePath, overwrite: true);
                _logger.LogDebug("Settings saved to {Path}.", _settingsFilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Persisting preferences must never crash the app; the session keeps running.
                _logger.LogError(ex, "Could not save settings to {Path}.", _settingsFilePath);
            }
        }
    }

    private UserSettings LoadOrDefaults()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new UserSettings();
        }

        try
        {
            string json = File.ReadAllText(_settingsFilePath);
            UserSettings? loaded = JsonSerializer.Deserialize<UserSettings>(json, SerializerOptions);
            if (loaded is null)
            {
                throw new JsonException("Settings file deserialized to null.");
            }

            _logger.LogInformation("Settings loaded from {Path}.", _settingsFilePath);
            return loaded;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            QuarantineCorruptFile(ex);
            return new UserSettings();
        }
    }

    private void QuarantineCorruptFile(Exception cause)
    {
        string quarantinePath = _settingsFilePath + ".corrupt";
        try
        {
            File.Move(_settingsFilePath, quarantinePath, overwrite: true);
            _logger.LogWarning(
                cause,
                "Settings file was unreadable; moved to {QuarantinePath} and defaults applied.",
                quarantinePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Settings file was unreadable and could not be quarantined.");
        }
    }
}
