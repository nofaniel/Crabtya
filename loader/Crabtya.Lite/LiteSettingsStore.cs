using System.Text.Json;
using BepInEx.Logging;

namespace Crabtya.Lite;

public static class LiteSettingsStore
{
    private const float DefaultCameraScale = 1f;
    private const bool DefaultInvertScroll = false;
    private const bool DefaultScrollZoomEnabled = true;

    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static ManualLogSource? _log;
    private static bool _loaded;
    private static LiteSettingsFile _settings = new();

    public static void Install(ManualLogSource log)
    {
        _log = log;
        EnsureLoaded();
    }

    public static float GetCameraScale()
    {
        EnsureLoaded();
        lock (Gate)
        {
            return ClampScale(_settings.CameraScale);
        }
    }

    public static void SetCameraScale(float value)
    {
        EnsureLoaded();
        lock (Gate)
        {
            var clamped = ClampScale(value);
            if (Math.Abs(_settings.CameraScale - clamped) < 0.0001f)
            {
                return;
            }

            _settings.CameraScale = clamped;
            Save();
        }
    }

    public static bool GetInvertScroll()
    {
        EnsureLoaded();
        lock (Gate)
        {
            return _settings.InvertScroll;
        }
    }

    public static void SetInvertScroll(bool value)
    {
        EnsureLoaded();
        lock (Gate)
        {
            if (_settings.InvertScroll == value)
            {
                return;
            }

            _settings.InvertScroll = value;
            Save();
        }
    }

    public static bool GetScrollZoomEnabled()
    {
        EnsureLoaded();
        lock (Gate)
        {
            return _settings.ScrollZoomEnabled;
        }
    }

    public static void SetScrollZoomEnabled(bool value)
    {
        EnsureLoaded();
        lock (Gate)
        {
            if (_settings.ScrollZoomEnabled == value)
            {
                return;
            }

            _settings.ScrollZoomEnabled = value;
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
                _settings = new LiteSettingsFile();
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<LiteSettingsFile>(json, JsonOptions);
                _settings = loaded ?? new LiteSettingsFile();
                _settings.CameraScale = ClampScale(_settings.CameraScale);
            }
            catch (Exception ex)
            {
                _log?.LogWarning($"Crabtya Lite settings load failed: {ex.Message}");
                _settings = new LiteSettingsFile();
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

            var json = JsonSerializer.Serialize(_settings, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"Crabtya Lite settings save failed: {ex.Message}");
        }
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "CrabtyaLite", "settings.json");
    }

    private static float ClampScale(float value)
    {
        if (!float.IsFinite(value))
        {
            return DefaultCameraScale;
        }

        return Math.Clamp(value, LiteCameraApplicator.MinScale, LiteCameraApplicator.MaxScale);
    }

    private sealed class LiteSettingsFile
    {
        public float CameraScale { get; set; } = DefaultCameraScale;

        public bool InvertScroll { get; set; } = DefaultInvertScroll;

        public bool ScrollZoomEnabled { get; set; } = DefaultScrollZoomEnabled;
    }
}
