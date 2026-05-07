using System.IO;
using System.Text.Json;
using QuickSType.Core.Config;
using QuickSType.Core.Hotkey;
using QuickSType.Core.Transcribe;

namespace QuickSType.UI.SmokeTest;

/// <summary>
/// HARDEN-05: validate the AOT-published binary launches, the hotkey hook constructs,
/// and Whisper decodes a known WAV. Per D-Q6 this DOES NOT validate real paste — that
/// lives in Phase 5's tests/manual/INJECTION_MATRIX.md. The transcript flows to stdout
/// for the CI grep step to verify.
///
/// Args:
///   args[0] (required): path to the fixture WAV (16 kHz mono float32)
///   args[1] (optional): path to smoke.expected.json (default: tests/fixtures/smoke.expected.json relative to fixture)
/// </summary>
public static class SmokeHarness
{
    public static int Run(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("smoke: expected fixture path as args[0]");
            return 2;
        }

        var fixturePath = args[0];
        if (!File.Exists(fixturePath))
        {
            Console.Error.WriteLine($"smoke: fixture not found: {fixturePath}");
            return 2;
        }

        var expectedPath = args.Length >= 2
            ? args[1]
            : Path.Combine(Path.GetDirectoryName(fixturePath) ?? ".", "smoke.expected.json");

        // 1. Hotkey-register check: instantiate HotkeyService (does not call RunAsync — that
        //    requires an OS hook which is not always available on a hosted runner; the
        //    constructor exercise is the AOT path we care about).
        try
        {
            using var hk = new HotkeyService("VcRightCtrl");
            // Do NOT call RunAsync — libuiohook on macos-13 hosted runner segfaults without
            // a display server / accessibility grant. Constructor exercise validates the
            // SharpHook AOT path; the hook is unit-tested in HotkeyParsingTests.
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"smoke: HotkeyService construction failed: {ex}");
            return 3;
        }

        // 2. Read the fixture WAV bytes; convert to 16 kHz mono float32 sample array.
        float[] samples;
        int sampleRate;
        try
        {
            (samples, sampleRate) = ReadFixtureWav(fixturePath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"smoke: WAV read failed: {ex}");
            return 4;
        }

        // 3. Locate ggml-tiny model. CI caches it in $HOME/quickstype-models/ggml-tiny.bin
        //    via actions/cache@v4; the path is set by an env var QUICKSTYPE_SMOKE_MODEL_PATH.
        var modelPath = Environment.GetEnvironmentVariable("QUICKSTYPE_SMOKE_MODEL_PATH")
                        ?? Path.Combine(ModelCatalog.ModelsDirectory(), "ggml-tiny.bin");
        if (!File.Exists(modelPath))
        {
            Console.Error.WriteLine($"smoke: ggml-tiny not found at {modelPath}; expected env QUICKSTYPE_SMOKE_MODEL_PATH or {ModelCatalog.PathFor("ggml-tiny")}");
            return 5;
        }

        string transcript;
        try
        {
            using var trans = new Transcriber();
            trans.EnsureLoaded(modelPath, useGpu: false);   // CPU path — hosted runners have no GPU
            var cfg = new AppConfig { Model = "ggml-tiny", ActiveLanguage = "en", TranscriptionBackend = "cpu" };
            transcript = trans.TranscribeAsync(samples.AsMemory(), sampleRate, cfg).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"smoke: TranscribeAsync failed: {ex}");
            return 6;
        }

        // 4. Validate against smoke.expected.json.
        SmokeExpected expected;
        try
        {
            var json = File.ReadAllText(expectedPath);
            expected = JsonSerializer.Deserialize(json, SmokeJsonContext.Default.SmokeExpected)
                ?? throw new InvalidOperationException("smoke.expected.json deserialized to null");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"smoke: expected.json load failed: {ex}");
            return 7;
        }

        var lower = transcript.Trim().ToLowerInvariant();

        // 5. Inject-into-stdout (the binary's text-emission code path; per D-Q6 this is
        //    the validated layer; real paste is Phase 5).
        Console.Out.WriteLine(transcript);
        Console.Out.Flush();

        // 6. Tolerance assertions (do AFTER stdout emission so CI logs always show the
        //    transcript even on failure).
        foreach (var token in expected.MustContainLowercase ?? new List<string>())
        {
            if (!lower.Contains(token))
            {
                Console.Error.WriteLine($"smoke: transcript missing required token '{token}': '{transcript}'");
                return 10;
            }
        }
        foreach (var bad in expected.MustNotContain ?? new List<string>())
        {
            if (lower.Contains(bad.ToLowerInvariant()))
            {
                Console.Error.WriteLine($"smoke: transcript contains forbidden token '{bad}' (Whisper hallucination?): '{transcript}'");
                return 11;
            }
        }
        if (expected.MaxLengthChars > 0 && transcript.Length > expected.MaxLengthChars)
        {
            Console.Error.WriteLine($"smoke: transcript length {transcript.Length} > max {expected.MaxLengthChars}");
            return 12;
        }

        return 0;
    }

    private static (float[] samples, int sampleRate) ReadFixtureWav(string path)
    {
        // Minimal RIFF/WAVE reader for 16 kHz mono float32 fixture. The project's existing
        // PortAudioCapture writes float32 internally; the fixture format matches.
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var br = new BinaryReader(fs);
        if (new string(br.ReadChars(4)) != "RIFF") throw new InvalidDataException("not RIFF");
        br.ReadInt32(); // file size
        if (new string(br.ReadChars(4)) != "WAVE") throw new InvalidDataException("not WAVE");

        int sampleRate = 0;
        short channels = 0;
        short bitsPerSample = 0;
        short formatTag = 0;
        byte[]? dataBytes = null;

        while (fs.Position < fs.Length)
        {
            var chunkId = new string(br.ReadChars(4));
            var chunkSize = br.ReadInt32();
            if (chunkId == "fmt ")
            {
                formatTag = br.ReadInt16();
                channels = br.ReadInt16();
                sampleRate = br.ReadInt32();
                br.ReadInt32();      // byte rate
                br.ReadInt16();      // block align
                bitsPerSample = br.ReadInt16();
                if (chunkSize > 16) br.ReadBytes(chunkSize - 16);
            }
            else if (chunkId == "data")
            {
                dataBytes = br.ReadBytes(chunkSize);
                break;
            }
            else
            {
                br.ReadBytes(chunkSize);
            }
        }

        if (dataBytes is null) throw new InvalidDataException("WAV has no data chunk");
        if (channels != 1) throw new InvalidDataException($"smoke fixture must be mono, got {channels} channels");
        if (sampleRate != 16000) throw new InvalidDataException($"smoke fixture must be 16 kHz, got {sampleRate}");

        // formatTag 3 = IEEE float; 1 = PCM. Project's TranscribeAsync expects float32.
        if (formatTag == 3 && bitsPerSample == 32)
        {
            var samples = new float[dataBytes.Length / 4];
            Buffer.BlockCopy(dataBytes, 0, samples, 0, dataBytes.Length);
            return (samples, sampleRate);
        }
        else if (formatTag == 1 && bitsPerSample == 16)
        {
            // PCM 16-bit -> normalized float
            var count = dataBytes.Length / 2;
            var samples = new float[count];
            for (int i = 0; i < count; i++)
                samples[i] = BitConverter.ToInt16(dataBytes, i * 2) / 32768f;
            return (samples, sampleRate);
        }
        throw new InvalidDataException($"unsupported WAV format: tag={formatTag}, bits={bitsPerSample}");
    }
}

public sealed record SmokeExpected
{
    [System.Text.Json.Serialization.JsonPropertyName("must_contain_lowercase")]
    public List<string>? MustContainLowercase { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("must_not_contain")]
    public List<string>? MustNotContain { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("max_length_chars")]
    public int MaxLengthChars { get; init; }
}
