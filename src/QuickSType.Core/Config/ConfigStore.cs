using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using IoPath = System.IO.Path;

namespace QuickSType.Core.Config;

public class ConfigStore
{
    private readonly string _filePath;
    private readonly string _legacyPath;
    private readonly ILogger _log;

    public ConfigStore(ILogger<ConfigStore>? log = null, string? overridePath = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
        _filePath = overridePath ?? DefaultPath();
        _legacyPath = IoPath.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "quickstype", "config.json");
    }

    public static string DefaultPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return IoPath.Combine(home, "Library", "Application Support", "QuickSType", "config.json");
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return IoPath.Combine(roaming, "QuickSType", "config.json");
        }
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
            ?? IoPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return IoPath.Combine(xdg, "quickstype", "config.json");
    }

    public string FilePath => _filePath;

    public AppConfig Load()
    {
        if (File.Exists(_filePath))
        {
            try
            {
                var json = File.ReadAllText(_filePath, Encoding.UTF8);
                var cfg = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppConfig);
                if (cfg is not null)
                {
                    if (cfg.SchemaVersion < 3)
                    {
                        _log.LogInformation("Migrating config from v{OldVersion} to v3", cfg.SchemaVersion);
                        cfg = MigrateV2ToV3(cfg);
                        Save(cfg);
                    }
                    return cfg;
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to read config at {Path}; falling back to defaults", _filePath);
            }
        }

        if (File.Exists(_legacyPath))
        {
            try
            {
                _log.LogInformation("Migrating legacy Python config from {Path}", _legacyPath);
                var legacyJson = File.ReadAllText(_legacyPath, Encoding.UTF8);
                var legacy = JsonSerializer.Deserialize(legacyJson, ConfigJsonContext.Default.LegacyPythonConfig);
                if (legacy is not null)
                {
                    var migrated = MigrateFromPython(legacy);
                    Save(migrated);
                    return migrated;
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Legacy config migration failed");
            }
        }

        return new AppConfig();
    }

    public void Save(AppConfig cfg)
    {
        var dir = IoPath.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = _filePath + ".tmp";
        var json = JsonSerializer.Serialize(cfg, ConfigJsonContext.Default.AppConfig);
        File.WriteAllText(tmp, json, Encoding.UTF8);
        if (File.Exists(_filePath)) File.Replace(tmp, _filePath, null);
        else File.Move(tmp, _filePath);
    }

    public static AppConfig MigrateFromPython(LegacyPythonConfig legacy)
    {
        return new AppConfig
        {
            Model = MapPythonModel(legacy.Model),
            Languages = legacy.Languages is { Count: > 0 } ? legacy.Languages : ["en"],
            ActiveLanguage = legacy.Languages is { Count: > 0 } ? legacy.Languages[0] : "en",
            Hotkey = MapPythonHotkey(legacy.Hotkey),
            AutoLanguage = legacy.AutoLanguage ?? false,
            SchemaVersion = 2,
        };
    }

    /// <summary>
    /// Migrate a v2 AppConfig to v3 by adding the four new fields with documented defaults
    /// and bumping SchemaVersion. Mirrors <see cref="MigrateFromPython"/> shape.
    /// </summary>
    public static AppConfig MigrateV2ToV3(AppConfig v2)
    {
        return v2 with
        {
            EnableCrashTelemetry = false,
            KeyboardLayoutDriven = false,
            StreamingMode = "auto",
            PreferredModel = null,
            SchemaVersion = 3,
        };
    }

    public static string MapPythonModel(string? python) => python switch
    {
        null => "ggml-base",
        "" => "ggml-base",
        "mlx-community/whisper-tiny" => "ggml-tiny",
        "mlx-community/whisper-base" => "ggml-base",
        "mlx-community/whisper-small" => "ggml-small",
        "mlx-community/whisper-medium" => "ggml-medium",
        "mlx-community/whisper-large-v3" => "ggml-large-v3",
        "mlx-community/whisper-large-v3-turbo" => "ggml-large-v3-turbo-q5_0",
        var s when s.StartsWith("ggml-") => s,
        _ => "ggml-base",
    };

    public static string MapPythonHotkey(string? python) => python switch
    {
        null => "VcRightAlt",
        "" => "VcRightAlt",
        "alt_r" => "VcRightAlt",
        "alt_l" => "VcLeftAlt",
        "alt" => "VcLeftAlt",
        "ctrl_r" => "VcRightControl",
        "ctrl_l" => "VcLeftControl",
        "ctrl" => "VcLeftControl",
        "cmd_r" or "right_cmd" => "VcRightMeta",
        "cmd_l" or "left_cmd" or "cmd" => "VcLeftMeta",
        "shift_r" => "VcRightShift",
        "shift_l" or "shift" => "VcLeftShift",
        "f5" => "VcF5",
        "f6" => "VcF6",
        "f13" => "VcF13",
        var s when s.StartsWith("Vc") => s,
        _ => "VcRightAlt",
    };
}
