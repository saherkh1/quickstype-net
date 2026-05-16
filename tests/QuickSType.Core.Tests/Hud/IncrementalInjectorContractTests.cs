using QuickSType.Core.Paste;
using QuickSType.Core.Transcribe;

namespace QuickSType.Core.Tests.Hud;

[Trait("category", "wave-0")]
public class IncrementalInjectorContractTests
{
    [Fact]
    public void AppendOnly_delta_has_zero_retract()
    {
        var update = new TranscriptUpdate(0, "hello");
        update.RetractChars.ShouldBe(0);
        update.AppendText.ShouldBe("hello");
    }

    [Fact]
    public void RetractOnly_delta_has_empty_append()
    {
        var update = new TranscriptUpdate(3, string.Empty);
        update.RetractChars.ShouldBe(3);
        update.AppendText.ShouldBeEmpty();
    }

    [Fact]
    public void RetractAndAppend_delta_combines_both()
    {
        var update = new TranscriptUpdate(2, "world");
        update.RetractChars.ShouldBe(2);
        update.AppendText.ShouldBe("world");
    }

    [Fact]
    public void Sequential_deltas_build_correct_text()
    {
        // Simulate applying sequential deltas to a text buffer
        var buffer = string.Empty;
        var deltas = new[]
        {
            new TranscriptUpdate(0, "helo"),
            new TranscriptUpdate(2, "llo world"),
        };
        foreach (var d in deltas)
        {
            if (d.RetractChars > 0)
                buffer = buffer.Length >= d.RetractChars
                    ? buffer[..^d.RetractChars]
                    : string.Empty;
            buffer += d.AppendText;
        }
        buffer.ShouldBe("hello world");
    }
}

[Trait("category", "hud-injector")]
public class FakeInjectorIntegrationTests
{
    private sealed class FakeInjector : IIncrementalInjector
    {
        public List<TranscriptUpdate> Applied { get; } = [];

        public Task ApplyAsync(TranscriptUpdate update, CancellationToken cancellationToken = default)
        {
            Applied.Add(update);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task FakeInjector_receives_all_sequential_deltas_in_order()
    {
        var received = new List<TranscriptUpdate>();
        var deltas = new[]
        {
            new TranscriptUpdate(0, "hello "),
            new TranscriptUpdate(0, "world"),
        };

        // Simulate the DictationEngine streaming loop behavior:
        // call ApplyAsync for each update in order
        var injector = new FakeInjector();
        foreach (var d in deltas)
            await injector.ApplyAsync(d, CancellationToken.None);

        injector.Applied.Count.ShouldBe(2);
        injector.Applied[0].AppendText.ShouldBe("hello ");
        injector.Applied[1].AppendText.ShouldBe("world");
    }

    [Fact]
    public async Task FakeInjector_skips_empty_updates_when_caller_filters()
    {
        var injector = new FakeInjector();
        // A noop update (0 retract, empty append) would be filtered by the injector guard
        var empty = new TranscriptUpdate(0, string.Empty);
        // Simulate engine guard: if (RetractChars <= 0 && IsNullOrEmpty(AppendText)) skip
        if (!(empty.RetractChars <= 0 && string.IsNullOrEmpty(empty.AppendText)))
            await injector.ApplyAsync(empty, CancellationToken.None);

        injector.Applied.ShouldBeEmpty();
    }

    [Fact]
    public async Task FakeInjector_retract_and_append_delta_recorded_correctly()
    {
        var injector = new FakeInjector();
        var update = new TranscriptUpdate(3, "world");
        await injector.ApplyAsync(update, CancellationToken.None);

        injector.Applied.Count.ShouldBe(1);
        injector.Applied[0].RetractChars.ShouldBe(3);
        injector.Applied[0].AppendText.ShouldBe("world");
    }
}
