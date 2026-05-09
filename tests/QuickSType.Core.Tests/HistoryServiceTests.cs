using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using QuickSType.Core.Config;
using QuickSType.Core.History;

namespace QuickSType.Core.Tests;

public class HistoryServiceTests
{
    [Fact]
    public void HistoryEntry_round_trips_through_source_gen()
    {
        var original = new HistoryEntry
        {
            V = 1,
            Ts = "2026-05-09T14:32:00Z",
            Text = "hello world",
            Model = "ggml-small",
            Lang = "en",
            DurationMs = 3200,
            Device = "Built-in Mic",
        };
        var json = JsonSerializer.Serialize(original, ConfigJsonContext.Default.HistoryEntry);
        var rt = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.HistoryEntry);
        rt.ShouldNotBeNull();
        rt!.V.ShouldBe(1);
        rt.Ts.ShouldBe("2026-05-09T14:32:00Z");
        rt.Text.ShouldBe("hello world");
        rt.Model.ShouldBe("ggml-small");
        rt.Lang.ShouldBe("en");
        rt.DurationMs.ShouldBe(3200);
        rt.Device.ShouldBe("Built-in Mic");
    }

    [Fact]
    public void Json_property_names_match_D_07_exactly()
    {
        var entry = new HistoryEntry { Text = "x", Model = "ggml-base", Lang = "en", Ts = "t" };
        var json = JsonSerializer.Serialize(entry, ConfigJsonContext.Default.HistoryEntry);
        json.ShouldContain("\"v\":");
        json.ShouldContain("\"ts\":");
        json.ShouldContain("\"text\":");
        json.ShouldContain("\"model\":");
        json.ShouldContain("\"lang\":");
        json.ShouldContain("\"duration_ms\":");
        json.ShouldContain("\"device\":");
    }

    [Fact]
    public void Default_HistoryEntry_V_is_1()
    {
        new HistoryEntry().V.ShouldBe(1);
    }

    [Fact]
    public async Task AppendAsync_writes_one_jsonl_line_per_call()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            File.Delete(tmp); // start fresh — AppendAsync should create
            var svc = new HistoryService(overridePath: tmp);
            await svc.AppendAsync(new HistoryEntry { Text = "first", Model = "ggml-base", Lang = "en", Ts = "t1" });
            await svc.AppendAsync(new HistoryEntry { Text = "second", Model = "ggml-base", Lang = "en", Ts = "t2" });

            var lines = await File.ReadAllLinesAsync(tmp);
            lines.Length.ShouldBe(2);

            var line0 = JsonSerializer.Deserialize(lines[0], ConfigJsonContext.Default.HistoryEntry);
            line0.ShouldNotBeNull();
            line0!.Text.ShouldBe("first");

            var line1 = JsonSerializer.Deserialize(lines[1], ConfigJsonContext.Default.HistoryEntry);
            line1!.Text.ShouldBe("second");
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public async Task AppendAsync_creates_directory_if_missing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qsttest-" + System.Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "history.jsonl");
        try
        {
            Directory.Exists(dir).ShouldBeFalse();
            var svc = new HistoryService(overridePath: path);
            await svc.AppendAsync(new HistoryEntry { Text = "x", Model = "ggml-base", Lang = "en", Ts = "t" });
            File.Exists(path).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task AppendAsync_swallows_io_failure_and_does_not_throw()
    {
        // Path that cannot be created — uses an invalid character on both Mac and Windows.
        // On Mac/Linux a NUL byte in the path is illegal. On Windows the same is true.
        var bogus = Path.Combine(Path.GetTempPath(), "qst\0invalid", "history.jsonl");
        var svc = new HistoryService(overridePath: bogus);
        // Must not throw — Pattern 7 wraps in try/catch and logs a warning.
        await svc.AppendAsync(new HistoryEntry { Text = "x", Model = "ggml-base", Lang = "en", Ts = "t" });
    }
}
