using ImuGui.Core.Models;

namespace ImuGui.Core.Pipeline;

/// <summary>
/// One fully processed frame. "Raw" throughout the UI means <em>unfiltered</em>: when
/// calibration is enabled it is already applied to both variants, so the raw/filtered
/// toggle isolates exactly the effect of the filter bank.
/// </summary>
/// <param name="Timestamp">The source timestamp of the underlying sample.</param>
/// <param name="DeltaTime">Measured time since the previous frame (zero for the first).</param>
/// <param name="RawSample">The unfiltered sample (calibrated when calibration is enabled).</param>
/// <param name="FilteredSample">The Kalman-filtered sample.</param>
/// <param name="RawOrientation">Attitude fused from the unfiltered stream.</param>
/// <param name="FilteredOrientation">Attitude fused from the filtered stream.</param>
public sealed record ProcessedFrame(
    TimeSpan Timestamp,
    TimeSpan DeltaTime,
    SensorSample RawSample,
    SensorSample FilteredSample,
    Orientation RawOrientation,
    Orientation FilteredOrientation);
