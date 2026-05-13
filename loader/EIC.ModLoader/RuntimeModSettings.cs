using System.Text.Json;

namespace EIC.ModLoader;

public static class RuntimeModSettings
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static Dictionary<string, float> _cameraPatchValues = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, PersistedSettingValue> _settingValues = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;

    public static bool TryGetFloat(string key, out float value)
    {
        EnsureLoaded();
        lock (Gate)
        {
            return _cameraPatchValues.TryGetValue(key, out value);
        }
    }

    public static void SetFloat(string key, float value)
    {
        EnsureLoaded();
        lock (Gate)
        {
            _cameraPatchValues[key] = value;
            Save();
        }
    }

    public static bool TryGetSettingBool(string key, out bool value)
    {
        EnsureLoaded();
        lock (Gate)
        {
            value = false;
            if (!_settingValues.TryGetValue(key, out var persisted) || !string.Equals(persisted.Kind, "bool", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!persisted.BoolValue.HasValue)
            {
                return false;
            }

            value = persisted.BoolValue.Value;
            return true;
        }
    }

    public static void SetSettingBool(string key, bool value)
    {
        EnsureLoaded();
        lock (Gate)
        {
            _settingValues[key] = new PersistedSettingValue
            {
                Kind = "bool",
                BoolValue = value
            };
            Save();
        }
    }

    public static bool TryGetSettingInt(string key, out int value)
    {
        EnsureLoaded();
        lock (Gate)
        {
            value = 0;
            if (!_settingValues.TryGetValue(key, out var persisted) || !string.Equals(persisted.Kind, "int", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!persisted.IntValue.HasValue)
            {
                return false;
            }

            value = persisted.IntValue.Value;
            return true;
        }
    }

    public static void SetSettingInt(string key, int value)
    {
        EnsureLoaded();
        lock (Gate)
        {
            _settingValues[key] = new PersistedSettingValue
            {
                Kind = "int",
                IntValue = value
            };
            Save();
        }
    }

    public static bool TryGetSettingFloat(string key, out float value)
    {
        EnsureLoaded();
        lock (Gate)
        {
            value = 0f;
            if (!_settingValues.TryGetValue(key, out var persisted) || !string.Equals(persisted.Kind, "float", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!persisted.FloatValue.HasValue)
            {
                return false;
            }

            value = persisted.FloatValue.Value;
            return true;
        }
    }

    public static void SetSettingFloat(string key, float value)
    {
        EnsureLoaded();
        lock (Gate)
        {
            _settingValues[key] = new PersistedSettingValue
            {
                Kind = "float",
                FloatValue = value
            };
            Save();
        }
    }

    public static bool TryGetSettingOption(string key, out string value)
    {
        EnsureLoaded();
        lock (Gate)
        {
            value = string.Empty;
            if (!_settingValues.TryGetValue(key, out var persisted) || !string.Equals(persisted.Kind, "option", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(persisted.OptionValue))
            {
                return false;
            }

            value = persisted.OptionValue;
            return true;
        }
    }

    public static void SetSettingOption(string key, string value)
    {
        EnsureLoaded();
        lock (Gate)
        {
            _settingValues[key] = new PersistedSettingValue
            {
                Kind = "option",
                OptionValue = value
            };
            Save();
        }
    }

    private static void EnsureLoaded()
    {
        lock (Gate)
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            var path = GetSettingsPath();
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                var file = JsonSerializer.Deserialize<RuntimeModSettingsFile>(json, JsonOptions);
                _cameraPatchValues = file?.CameraPatchValues is null
                    ? new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, float>(file.CameraPatchValues, StringComparer.OrdinalIgnoreCase);
                _settingValues = file?.ModSettingValues is null
                    ? new Dictionary<string, PersistedSettingValue>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, PersistedSettingValue>(file.ModSettingValues, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Plugin.Instance?.Log.LogWarning($"Runtime mod settings load failed: {ex.Message}");
                _cameraPatchValues = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                _settingValues = new Dictionary<string, PersistedSettingValue>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private static void Save()
    {
        try
        {
            var path = GetSettingsPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var file = new RuntimeModSettingsFile
            {
                CameraPatchValues = new Dictionary<string, float>(_cameraPatchValues, StringComparer.OrdinalIgnoreCase),
                ModSettingValues = new Dictionary<string, PersistedSettingValue>(_settingValues, StringComparer.OrdinalIgnoreCase)
            };
            File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOptions));
        }
        catch (Exception ex)
        {
            Plugin.Instance?.Log.LogWarning($"Runtime mod settings save failed: {ex.Message}");
        }
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Mods", "runtime-settings.json");
    }

    private sealed class RuntimeModSettingsFile
    {
        public Dictionary<string, float> CameraPatchValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, PersistedSettingValue> ModSettingValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class PersistedSettingValue
    {
        public string Kind { get; set; } = string.Empty;

        public bool? BoolValue { get; set; }

        public int? IntValue { get; set; }

        public float? FloatValue { get; set; }

        public string OptionValue { get; set; } = string.Empty;
    }
}
