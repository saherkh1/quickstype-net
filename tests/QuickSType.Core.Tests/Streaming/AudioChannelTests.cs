using System.Threading.Channels;
using QuickSType.Core.Audio;
using Xunit;
using Shouldly;

namespace QuickSType.Core.Tests.Streaming;

public class AudioChannelTests
{
    [Fact]
    public void Frames_returns_null_when_not_recording()
    {
        PortAudioCapture? cap = null;
        try { cap = new PortAudioCapture(); }
        catch { return; } // No audio device in CI; skip gracefully

        cap.IsRecording.ShouldBeFalse();
        cap.Frames.ShouldBeNull();
        cap.Dispose();
    }

    [Fact]
    public async Task Channel_bounded_dropoldest_does_not_throw_under_overflow()
    {
        var ch = Channel.CreateBounded<ReadOnlyMemory<float>>(new BoundedChannelOptions(2)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true,
        });

        ch.Writer.TryWrite(new ReadOnlyMemory<float>(new float[] { 1f })).ShouldBeTrue();
        ch.Writer.TryWrite(new ReadOnlyMemory<float>(new float[] { 2f })).ShouldBeTrue();
        ch.Writer.TryWrite(new ReadOnlyMemory<float>(new float[] { 3f })).ShouldBeTrue(); // drops oldest
        ch.Writer.TryComplete();

        var items = new List<float>();
        await foreach (var mem in ch.Reader.ReadAllAsync())
            items.Add(mem.Span[0]);

        items.Count.ShouldBe(2);
        items.ShouldContain(2f);
        items.ShouldContain(3f);
    }
}
