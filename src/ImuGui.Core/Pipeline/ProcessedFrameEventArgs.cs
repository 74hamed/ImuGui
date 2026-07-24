namespace ImuGui.Core.Pipeline;

/// <summary>Carries one <see cref="ProcessedFrame"/>.</summary>
public sealed class ProcessedFrameEventArgs : EventArgs
{
    /// <summary>Creates the event args.</summary>
    /// <param name="frame">The processed frame.</param>
    public ProcessedFrameEventArgs(ProcessedFrame frame) =>
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));

    /// <summary>The processed frame.</summary>
    public ProcessedFrame Frame { get; }
}
