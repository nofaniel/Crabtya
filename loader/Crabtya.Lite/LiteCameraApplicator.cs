using System.Globalization;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace Crabtya.Lite;

public static class LiteCameraApplicator
{
    public const float MinScale = 0.6f;
    public const float MaxScale = 2.2f;
    public const float StepScale = 0.05f;
    private const float MinPerspectiveFieldOfView = 5f;
    private const float MaxPerspectiveFieldOfView = 170f;
    private const float CameraRescanIntervalSeconds = 0.75f;

    private static readonly Dictionary<int, float> BaselineOrthographicSizes = new();
    private static readonly Dictionary<int, float> BaselineFieldOfViews = new();
    private static readonly List<Camera> CachedCameras = new();
    private static readonly HashSet<int> LoggedPerspectiveFallback = new();

    private static ManualLogSource? _log;
    private static float _nextCameraRescanTime;
    private static float _lastLoggedScale = float.NaN;

    private static bool _reflectionInitialized;
    private static PropertyInfo? _isMouseCurrentProp;
    private static PropertyInfo? _isMouseScrollProp;
    private static MethodInfo? _isScrollReadValueMethod;
    private static PropertyInfo? _isScrollYProp;
    private static PropertyInfo? _liScrollDeltaProp;
    private static PropertyInfo? _liScrollDeltaYProp;
    private static MethodInfo? _liGetAxisMethod;

    public static void Install(ManualLogSource log)
    {
        _log = log;
        _log.LogInfo("Crabtya Lite camera applicator installed.");
    }

    public static void Update()
    {
        if (LiteNativeSettingsApplicator.IsVisible)
        {
            return;
        }

        if (!LiteSettingsStore.GetScrollZoomEnabled())
        {
            return;
        }

        var wheelDelta = ReadMouseWheelDelta();
        if (Math.Abs(wheelDelta) < 0.001f)
        {
            return;
        }

        var direction = Math.Sign(wheelDelta);
        if (direction == 0)
        {
            return;
        }

        if (LiteSettingsStore.GetInvertScroll())
        {
            direction = -direction;
        }

        var current = LiteSettingsStore.GetCameraScale();
        var next = Math.Clamp(Quantize(current + direction * StepScale, StepScale), MinScale, MaxScale);
        if (Math.Abs(current - next) < 0.0001f)
        {
            return;
        }

        LiteSettingsStore.SetCameraScale(next);
        ApplyToLiveCameras();

        if (float.IsNaN(_lastLoggedScale) || Math.Abs(_lastLoggedScale - next) >= StepScale)
        {
            _lastLoggedScale = next;
            _log?.LogInfo($"Crabtya Lite camera scale set to {next.ToString("0.##", CultureInfo.InvariantCulture)}x.");
        }
    }

    public static void LateUpdate()
    {
        ApplyToLiveCameras();
    }

    public static void ApplyToLiveCameras()
    {
        var cameras = GetLiveCameras();
        if (cameras.Count == 0)
        {
            return;
        }

        var scale = LiteSettingsStore.GetCameraScale();
        for (var i = 0; i < cameras.Count; i++)
        {
            var camera = cameras[i];
            var key = camera.GetInstanceID();

            if (camera.orthographic)
            {
                if (!BaselineOrthographicSizes.TryGetValue(key, out var baseline))
                {
                    baseline = camera.orthographicSize;
                    BaselineOrthographicSizes[key] = baseline;
                }

                var desired = Math.Max(0.001f, baseline * scale);
                if (Math.Abs(camera.orthographicSize - desired) >= 0.001f)
                {
                    camera.orthographicSize = desired;
                    camera.ResetProjectionMatrix();
                }

                continue;
            }

            if (!BaselineFieldOfViews.TryGetValue(key, out var baselineFov))
            {
                baselineFov = camera.fieldOfView;
                BaselineFieldOfViews[key] = baselineFov;
            }

            var desiredFov = Math.Clamp(baselineFov / scale, MinPerspectiveFieldOfView, MaxPerspectiveFieldOfView);
            if (Math.Abs(camera.fieldOfView - desiredFov) >= 0.001f)
            {
                camera.fieldOfView = desiredFov;
                camera.ResetProjectionMatrix();
            }

            if (LoggedPerspectiveFallback.Add(key))
            {
                _log?.LogInfo(
                    $"Crabtya Lite: applying camera scale fallback through Camera.fieldOfView for '{camera.name}' because orthographic mode is false.");
            }
        }
    }

    private static IReadOnlyList<Camera> GetLiveCameras()
    {
        if (Time.unscaledTime >= _nextCameraRescanTime || CachedCameras.Count == 0)
        {
            RefreshCameraCache();
        }
        else
        {
            for (var i = CachedCameras.Count - 1; i >= 0; i--)
            {
                var camera = CachedCameras[i];
                var keep = false;
                try
                {
                    keep = camera is not null && camera.isActiveAndEnabled && camera.gameObject.activeInHierarchy;
                }
                catch
                {
                }

                if (!keep)
                {
                    CachedCameras.RemoveAt(i);
                }
            }
        }

        return CachedCameras;
    }

    private static void RefreshCameraCache()
    {
        _nextCameraRescanTime = Time.unscaledTime + CameraRescanIntervalSeconds;
        CachedCameras.Clear();

        Camera[] scanned;
        try
        {
            scanned = UnityEngine.Object.FindObjectsOfType<Camera>();
        }
        catch
        {
            return;
        }

        var currentIds = new HashSet<int>();
        for (var i = 0; i < scanned.Length; i++)
        {
            var camera = scanned[i];
            var live = false;
            try
            {
                live = camera is not null && camera.isActiveAndEnabled && camera.gameObject.activeInHierarchy;
            }
            catch
            {
            }

            if (!live)
            {
                continue;
            }

            CachedCameras.Add(camera);
            currentIds.Add(camera.GetInstanceID());
        }

        foreach (var key in BaselineOrthographicSizes.Keys.Where(id => !currentIds.Contains(id)).ToList())
        {
            BaselineOrthographicSizes.Remove(key);
        }

        foreach (var key in BaselineFieldOfViews.Keys.Where(id => !currentIds.Contains(id)).ToList())
        {
            BaselineFieldOfViews.Remove(key);
            LoggedPerspectiveFallback.Remove(key);
        }
    }

    private static float ReadMouseWheelDelta()
    {
        if (!_reflectionInitialized)
        {
            InitializeReflection();
        }

        var delta = TryReadInputSystemMouseWheelDelta();
        if (Math.Abs(delta) >= 0.001f)
        {
            return delta;
        }

        return TryReadLegacyInputMouseWheelDelta();
    }

    private static void InitializeReflection()
    {
        _reflectionInitialized = true;

        var mouseType = ResolveType("UnityEngine.InputSystem.Mouse");
        if (mouseType is not null)
        {
            _isMouseCurrentProp = mouseType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            _isMouseScrollProp = mouseType.GetProperty("scroll", BindingFlags.Public | BindingFlags.Instance);
        }

        var inputType = ResolveType("UnityEngine.Input");
        if (inputType is not null)
        {
            _liScrollDeltaProp = inputType.GetProperty("mouseScrollDelta", BindingFlags.Public | BindingFlags.Static);
            _liGetAxisMethod = inputType.GetMethod("GetAxis", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
        }
    }

    private static float TryReadInputSystemMouseWheelDelta()
    {
        if (_isMouseCurrentProp is null)
        {
            return 0f;
        }

        try
        {
            var current = _isMouseCurrentProp.GetValue(null);
            if (current is null)
            {
                return 0f;
            }

            var scrollControl = _isMouseScrollProp?.GetValue(current);
            if (scrollControl is null)
            {
                return 0f;
            }

            if (_isScrollReadValueMethod is null)
            {
                _isScrollReadValueMethod = scrollControl.GetType().GetMethod(
                    "ReadValue",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);
                if (_isScrollReadValueMethod is null)
                {
                    return 0f;
                }
            }

            var vector = _isScrollReadValueMethod.Invoke(scrollControl, null);
            return ReadY(ref _isScrollYProp, vector);
        }
        catch
        {
            return 0f;
        }
    }

    private static float TryReadLegacyInputMouseWheelDelta()
    {
        if (_liScrollDeltaProp is null && _liGetAxisMethod is null)
        {
            return 0f;
        }

        try
        {
            if (_liScrollDeltaProp is not null)
            {
                var scrollDelta = _liScrollDeltaProp.GetValue(null);
                var delta = ReadY(ref _liScrollDeltaYProp, scrollDelta);
                if (Math.Abs(delta) >= 0.001f)
                {
                    return delta;
                }
            }
        }
        catch
        {
        }

        try
        {
            if (_liGetAxisMethod is not null)
            {
                var axisValue = _liGetAxisMethod.Invoke(null, new object[] { "Mouse ScrollWheel" });
                if (axisValue is float axisDelta)
                {
                    return axisDelta;
                }
            }
        }
        catch
        {
            return 0f;
        }

        return 0f;
    }

    private static float ReadY(ref PropertyInfo? cachedProp, object? vectorLike)
    {
        if (vectorLike is null)
        {
            return 0f;
        }

        try
        {
            if (cachedProp is null)
            {
                cachedProp = vectorLike.GetType().GetProperty("y", BindingFlags.Public | BindingFlags.Instance);
            }

            if (cachedProp is not null)
            {
                var value = cachedProp.GetValue(vectorLike);
                if (value is float f)
                {
                    return f;
                }

                if (value is double d)
                {
                    return (float)d;
                }
            }

            var field = vectorLike.GetType().GetField("y", BindingFlags.Public | BindingFlags.Instance);
            if (field is not null)
            {
                var value = field.GetValue(vectorLike);
                if (value is float f)
                {
                    return f;
                }

                if (value is double d)
                {
                    return (float)d;
                }
            }
        }
        catch
        {
            return 0f;
        }

        return 0f;
    }

    private static Type? ResolveType(string fullName)
    {
        var type = Type.GetType(fullName, throwOnError: false, ignoreCase: false);
        if (type is not null)
        {
            return type;
        }

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (var i = 0; i < assemblies.Length; i++)
        {
            try
            {
                type = assemblies[i].GetType(fullName, throwOnError: false, ignoreCase: false);
                if (type is not null)
                {
                    return type;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static float Quantize(float value, float step)
    {
        return step <= 0f ? value : (float)Math.Round(value / step) * step;
    }
}
