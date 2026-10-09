namespace SystemAudioAnalyzer.Core;

/// <summary>Reads the current master (endpoint) volume gain and mute state. A seam over
/// NAudio's <c>AudioEndpointVolume</c> so capture gain logic can be unit-tested without real COM objects.</summary>
public interface IEndpointVolumeReader
{
    float Scalar { get; }

    bool IsMuted { get; }
}
