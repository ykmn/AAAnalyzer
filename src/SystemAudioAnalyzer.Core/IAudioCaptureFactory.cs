namespace SystemAudioAnalyzer.Core;

public interface IAudioCaptureFactory
{
    IAudioCapture Create(OutputDeviceInfo device, FaderMode faderMode = FaderMode.PreFader);
}
