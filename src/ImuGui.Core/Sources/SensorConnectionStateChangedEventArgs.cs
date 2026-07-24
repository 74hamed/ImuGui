using ImuGui.Core.Models;

namespace ImuGui.Core.Sources;

/// <summary>Describes a connection-state transition of a sensor source.</summary>
public sealed class SensorConnectionStateChangedEventArgs : EventArgs
{
    /// <summary>Creates the event args.</summary>
    /// <param name="previousState">The state before the transition.</param>
    /// <param name="newState">The state after the transition.</param>
    public SensorConnectionStateChangedEventArgs(
        SensorConnectionState previousState, SensorConnectionState newState)
    {
        PreviousState = previousState;
        NewState = newState;
    }

    /// <summary>The state before the transition.</summary>
    public SensorConnectionState PreviousState { get; }

    /// <summary>The state after the transition.</summary>
    public SensorConnectionState NewState { get; }
}
