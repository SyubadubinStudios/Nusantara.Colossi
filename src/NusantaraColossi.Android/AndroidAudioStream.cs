using Android.Media;
using ThreeNet;

namespace NusantaraColossi.Android;

/// <summary>
/// Streams an offline ThreeNet <see cref="AudioEngine"/> to an Android <see cref="AudioTrack"/> on a
/// background thread. ThreeNet's own device output can't start on Android (its native audio backend
/// aborts without an NDK context), so the game mixes offline and this pushes the samples out.
/// </summary>
internal sealed class AndroidAudioStream : IDisposable
{
    private const int FramesPerChunk = 960; // 20 ms at 48 kHz
    private readonly AudioEngine engine;
    private readonly AudioTrack track;
    private readonly Thread thread;
    private volatile bool running = true;
    private volatile bool paused;

    public static AndroidAudioStream? Current { get; private set; }

    public AndroidAudioStream(AudioEngine engine, int sampleRate)
    {
        this.engine = engine;
        var minBytes = AudioTrack.GetMinBufferSize(sampleRate, ChannelOut.Stereo, Encoding.PcmFloat);
        track = new AudioTrack.Builder()
            .SetAudioAttributes(new AudioAttributes.Builder().SetUsage(AudioUsageKind.Game)!.SetContentType(AudioContentType.Music)!.Build()!)!
            .SetAudioFormat(new AudioFormat.Builder().SetEncoding(Encoding.PcmFloat)!.SetSampleRate(sampleRate)!.SetChannelMask(ChannelOut.Stereo)!.Build()!)!
            .SetBufferSizeInBytes(Math.Max(minBytes, FramesPerChunk * 2 * sizeof(float) * 3))!
            .SetTransferMode(AudioTrackMode.Stream)!
            .Build()!;
        track.Play();
        thread = new Thread(Pump) { IsBackground = true, Name = "nc-audio", Priority = ThreadPriority.AboveNormal };
        thread.Start();
        Current = this;
    }

    private void Pump()
    {
        var buffer = new float[FramesPerChunk * 2];
        while (running)
        {
            if (paused) { Thread.Sleep(20); continue; }
            try { engine.Render(buffer); }
            catch (ObjectDisposedException) { break; }
            // Blocking write paces the loop to the device clock.
            track.Write(buffer, 0, buffer.Length, WriteMode.Blocking);
        }
    }

    /// <summary>Activity lifecycle: silence while the app is in the background.</summary>
    public void SetPaused(bool value)
    {
        paused = value;
        if (value) track.Pause(); else track.Play();
    }

    public void Dispose()
    {
        running = false;
        thread.Join(500);
        track.Stop();
        track.Release();
        if (Current == this) Current = null;
    }
}
