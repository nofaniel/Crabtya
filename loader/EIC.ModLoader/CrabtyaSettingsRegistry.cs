using Crabtya.ModApi;

namespace EIC.ModLoader;

public static class CrabtyaSettingsRegistry
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, ModSettingRegistration> SettingsByStorageKey = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> LoggedUnsupportedKinds = new(StringComparer.OrdinalIgnoreCase);

    public static void Reset()
    {
        lock (Gate)
        {
            SettingsByStorageKey.Clear();
            LoggedUnsupportedKinds.Clear();
        }
    }

    public static ICrabtyaSettingsRegistry CreateForMod(string modId, string modName, string modVersion)
    {
        return new ModScopedSettingsRegistry(modId, modName, modVersion);
    }

    public static IReadOnlyList<ModSettingRegistration> GetAllSettings()
    {
        lock (Gate)
        {
            return SettingsByStorageKey.Values
                .OrderBy(v => v.ModId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(v => v.Definition.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public static bool TryGetSetting(string storageKey, out ModSettingRegistration registration)
    {
        lock (Gate)
        {
            return SettingsByStorageKey.TryGetValue(storageKey, out registration!);
        }
    }

    public static bool TryGetBool(string storageKey, out bool value)
    {
        if (RuntimeModSettings.TryGetSettingBool(storageKey, out value))
        {
            return true;
        }

        if (TryGetSetting(storageKey, out var registration) && registration.Definition.DefaultValue is bool defaultBool)
        {
            value = defaultBool;
            RuntimeModSettings.SetSettingBool(storageKey, value);
            return true;
        }

        value = false;
        return false;
    }

    public static bool TryGetInt(string storageKey, out int value)
    {
        if (RuntimeModSettings.TryGetSettingInt(storageKey, out value))
        {
            return true;
        }

        if (TryGetSetting(storageKey, out var registration) && registration.Definition.DefaultValue is int defaultInt)
        {
            value = defaultInt;
            RuntimeModSettings.SetSettingInt(storageKey, value);
            return true;
        }

        value = 0;
        return false;
    }

    public static bool TryGetFloat(string storageKey, out float value)
    {
        if (RuntimeModSettings.TryGetSettingFloat(storageKey, out value))
        {
            return true;
        }

        if (TryGetSetting(storageKey, out var registration) && registration.Definition.DefaultValue is float defaultFloat)
        {
            value = defaultFloat;
            RuntimeModSettings.SetSettingFloat(storageKey, value);
            return true;
        }

        value = 0f;
        return false;
    }

    public static bool TryGetOption(string storageKey, out string value)
    {
        if (RuntimeModSettings.TryGetSettingOption(storageKey, out value))
        {
            return true;
        }

        if (TryGetSetting(storageKey, out var registration) && registration.Definition.DefaultValue is string defaultOption)
        {
            value = defaultOption;
            RuntimeModSettings.SetSettingOption(storageKey, value);
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static void RegisterSetting(ModSettingRegistration registration)
    {
        lock (Gate)
        {
            SettingsByStorageKey[registration.StorageKey] = registration;
        }
    }

    private sealed class ModScopedSettingsRegistry : ICrabtyaSettingsRegistry
    {
        private readonly string _modId;
        private readonly string _modName;
        private readonly string _modVersion;

        public ModScopedSettingsRegistry(string modId, string modName, string modVersion)
        {
            _modId = modId;
            _modName = modName;
            _modVersion = modVersion;
        }

        public void RegisterBool(string key, string label, bool defaultValue, string description = "")
        {
            var definition = new CrabtyaSettingDefinition
            {
                Key = key,
                Label = label,
                Description = description,
                Kind = CrabtyaSettingKind.Bool,
                DefaultValue = defaultValue
            };
            var storageKey = BuildStorageKey(_modId, key);
            RegisterSetting(new ModSettingRegistration(_modId, _modName, _modVersion, storageKey, definition));
            if (!RuntimeModSettings.TryGetSettingBool(storageKey, out _))
            {
                RuntimeModSettings.SetSettingBool(storageKey, defaultValue);
            }
        }

        public void RegisterInt(string key, string label, int defaultValue, int min, int max, int step = 1, string description = "")
        {
            var normalizedStep = step <= 0 ? 1 : step;
            var normalizedMin = Math.Min(min, max);
            var normalizedMax = Math.Max(min, max);
            var normalizedDefault = Math.Clamp(defaultValue, normalizedMin, normalizedMax);
            var definition = new CrabtyaSettingDefinition
            {
                Key = key,
                Label = label,
                Description = description,
                Kind = CrabtyaSettingKind.Int,
                DefaultValue = normalizedDefault,
                IntMin = normalizedMin,
                IntMax = normalizedMax,
                IntStep = normalizedStep
            };
            var storageKey = BuildStorageKey(_modId, key);
            RegisterSetting(new ModSettingRegistration(_modId, _modName, _modVersion, storageKey, definition));
            if (!RuntimeModSettings.TryGetSettingInt(storageKey, out _))
            {
                RuntimeModSettings.SetSettingInt(storageKey, normalizedDefault);
            }
        }

        public void RegisterFloat(string key, string label, float defaultValue, float min, float max, float step = 0.05f, string description = "")
        {
            var normalizedStep = step <= 0f ? 0.05f : step;
            var normalizedMin = Math.Min(min, max);
            var normalizedMax = Math.Max(min, max);
            var normalizedDefault = Math.Clamp(defaultValue, normalizedMin, normalizedMax);
            var definition = new CrabtyaSettingDefinition
            {
                Key = key,
                Label = label,
                Description = description,
                Kind = CrabtyaSettingKind.Float,
                DefaultValue = normalizedDefault,
                FloatMin = normalizedMin,
                FloatMax = normalizedMax,
                FloatStep = normalizedStep
            };
            var storageKey = BuildStorageKey(_modId, key);
            RegisterSetting(new ModSettingRegistration(_modId, _modName, _modVersion, storageKey, definition));
            if (!RuntimeModSettings.TryGetSettingFloat(storageKey, out _))
            {
                RuntimeModSettings.SetSettingFloat(storageKey, normalizedDefault);
            }
        }

        public void RegisterOption(string key, string label, string defaultValue, IReadOnlyList<string> options, string description = "")
        {
            var normalizedOptions = (options ?? Array.Empty<string>())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (normalizedOptions.Count == 0)
            {
                normalizedOptions.Add(defaultValue);
            }

            var normalizedDefault = normalizedOptions.Contains(defaultValue, StringComparer.OrdinalIgnoreCase)
                ? normalizedOptions.First(v => string.Equals(v, defaultValue, StringComparison.OrdinalIgnoreCase))
                : normalizedOptions[0];
            var definition = new CrabtyaSettingDefinition
            {
                Key = key,
                Label = label,
                Description = description,
                Kind = CrabtyaSettingKind.Option,
                DefaultValue = normalizedDefault,
                Options = normalizedOptions
            };
            var storageKey = BuildStorageKey(_modId, key);
            RegisterSetting(new ModSettingRegistration(_modId, _modName, _modVersion, storageKey, definition));
            if (!RuntimeModSettings.TryGetSettingOption(storageKey, out _))
            {
                RuntimeModSettings.SetSettingOption(storageKey, normalizedDefault);
            }
        }

        public bool TryGetBool(string key, out bool value)
        {
            return CrabtyaSettingsRegistry.TryGetBool(BuildStorageKey(_modId, key), out value);
        }

        public bool TryGetInt(string key, out int value)
        {
            return CrabtyaSettingsRegistry.TryGetInt(BuildStorageKey(_modId, key), out value);
        }

        public bool TryGetFloat(string key, out float value)
        {
            return CrabtyaSettingsRegistry.TryGetFloat(BuildStorageKey(_modId, key), out value);
        }

        public bool TryGetOption(string key, out string value)
        {
            return CrabtyaSettingsRegistry.TryGetOption(BuildStorageKey(_modId, key), out value);
        }

        public void SetBool(string key, bool value)
        {
            RuntimeModSettings.SetSettingBool(BuildStorageKey(_modId, key), value);
        }

        public void SetInt(string key, int value)
        {
            var storageKey = BuildStorageKey(_modId, key);
            if (TryGetSetting(storageKey, out var registration))
            {
                var min = registration.Definition.IntMin ?? int.MinValue;
                var max = registration.Definition.IntMax ?? int.MaxValue;
                value = Math.Clamp(value, min, max);
            }

            RuntimeModSettings.SetSettingInt(storageKey, value);
        }

        public void SetFloat(string key, float value)
        {
            var storageKey = BuildStorageKey(_modId, key);
            if (TryGetSetting(storageKey, out var registration))
            {
                var min = registration.Definition.FloatMin ?? float.MinValue;
                var max = registration.Definition.FloatMax ?? float.MaxValue;
                value = Math.Clamp(value, min, max);
            }

            RuntimeModSettings.SetSettingFloat(storageKey, value);
        }

        public void SetOption(string key, string value)
        {
            var storageKey = BuildStorageKey(_modId, key);
            if (TryGetSetting(storageKey, out var registration))
            {
                var options = registration.Definition.Options;
                if (options.Count > 0)
                {
                    var chosen = options.FirstOrDefault(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase));
                    value = string.IsNullOrWhiteSpace(chosen) ? options[0] : chosen;
                }
            }

            RuntimeModSettings.SetSettingOption(storageKey, value);
        }

        private static string BuildStorageKey(string modId, string key)
        {
            var normalizedKey = string.IsNullOrWhiteSpace(key) ? "setting" : key.Trim();
            return $"{modId}:{normalizedKey}";
        }
    }
}

public sealed record ModSettingRegistration(
    string ModId,
    string ModName,
    string ModVersion,
    string StorageKey,
    CrabtyaSettingDefinition Definition);
