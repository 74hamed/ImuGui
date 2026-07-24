namespace ImuGui.App.Settings;

/// <summary>Loads, mutates, and persists <see cref="UserSettings"/>.</summary>
public interface ISettingsService
{
    /// <summary>The current settings snapshot.</summary>
    UserSettings Current { get; }

    /// <summary>Applies a functional mutation to the current snapshot (not yet persisted).</summary>
    /// <param name="mutate">Produces the new snapshot from the old one.</param>
    void Update(Func<UserSettings, UserSettings> mutate);

    /// <summary>Persists the current snapshot to disk.</summary>
    void Save();
}
