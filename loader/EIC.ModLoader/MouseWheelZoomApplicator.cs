using System.Globalization;
using System.Reflection;
using System.Text.Json;
using UnityEngine;

namespace EIC.ModLoader;

public static class MouseWheelZoomApplicator
{
    private const string SupportedTarget = "camera.orthographicSize";
    private static float _lastLoggedValue = float.NaN;

    // Reflection cache — populated once on the first frame that needs input.
    private static bool _reflectionInitialized;
    // InputSystem path
    private static PropertyInfo? _isMouseCurrentProp;
    private static PropertyInfo? _isMouseScrollProp;
    private static MethodInfo? _isScrollReadValueMethod;  // lazy: set on first valid scrollControl
    private static PropertyInfo? _isScrollYProp;          // lazy: set on first valid vector
    // Legacy Input path
    private static PropertyInfo? _liScrollDeltaProp;
    private static PropertyInfo? _liScrollDeltaYProp;
    private static MethodInfo? _liGetAxisMethod;

    // Patch cache — invalidated when CameraPatches.Count changes.
    private static CameraPatchDefinition? _cachedPatch;
    private static int _cachedPatchCount = -1;

    public static void Update()
    {
        // Do not apply camera zoom while the Crabtya Mods settings window is open;
        // the scroll input belongs to the mod list ScrollRect in that case.
        if (CrabtyaModsSettingsWindow.IsVisible)
        {
            return;
        }

        var patch = GetAdjustableMouseWheelPatch();
        if (patch is null)
        {
            return;
        }

        var wheelDelta = ReadMouseWheelDelta();
        if (Math.Abs(wheelDelta) < 0.001f)
        {
            return;
        }

        var min = GetMin(patch);
        var max = GetMax(patch);
        var step = GetStep(patch);
        var current = Mathf.Clamp(Quantize(GetCurrentValue(patch), step), min, max);
        var direction = Math.Sign(wheelDelta);
        if (direction == 0)
        {
            return;
        }

        if (IsInverted(patch))
        {
            direction = -direction;
        }

        var next = Mathf.Clamp(Quantize(current + direction * step, step), min, max);
        if (Math.Abs(next - current) < 0.0001f)
        {
            return;
        }

        RuntimeModSettings.SetFloat(patch.Id, next);
        CameraPatchApplicator.ApplyToLiveCameras();

        if (float.IsNaN(_lastLoggedValue) || Math.Abs(_lastLoggedValue - next) >= step)
        {
            _lastLoggedValue = next;
            Plugin.Instance?.Log.LogInfo(
                $"Runtime overlay mouse-wheel zoom: set '{patch.Id}' to {next.ToString("0.##", CultureInfo.InvariantCulture)}x.");
        }
    }

    private static CameraPatchDefinition? GetAdjustableMouseWheelPatch()
    {
        var patches = RuntimeContentState.Current.CameraPatches;
        var count = patches.Count;
        if (count == _cachedPatchCount)
        {
            return _cachedPatch;
        }

        _cachedPatchCount = count;
        _cachedPatch = null;
        foreach (var p in patches.Values)
        {
            if (p.Settings.MouseWheel &&
                string.Equals(p.Target, SupportedTarget, StringComparison.OrdinalIgnoreCase))
            {
                _cachedPatch = p;
            }
        }

        return _cachedPatch;
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
                    "ReadValue", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
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
            // fall through to GetAxis
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
                if (value is float f) return f;
                if (value is double d) return (float)d;
            }

            var field = vectorLike.GetType().GetField("y", BindingFlags.Public | BindingFlags.Instance);
            if (field is not null)
            {
                var value = field.GetValue(vectorLike);
                if (value is float f) return f;
                if (value is double d) return (float)d;
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
                // continue scan
            }
        }

        return null;
    }

    private static float GetCurrentValue(CameraPatchDefinition patch)
    {
        if (RuntimeModSettings.TryGetFloat(patch.Id, out var value))
        {
            return value;
        }

        return TryGetNumericValue(patch.Value, out value) ? value : 1f;
    }

    private static float GetMin(CameraPatchDefinition patch)
    {
        return patch.Settings.Min > 0f ? patch.Settings.Min : 0.75f;
    }

    private static float GetMax(CameraPatchDefinition patch)
    {
        var min = GetMin(patch);
        return patch.Settings.Max > min ? patch.Settings.Max : min + 1f;
    }

    private static float GetStep(CameraPatchDefinition patch)
    {
        return patch.Settings.Step > 0f ? patch.Settings.Step : 0.05f;
    }

    internal static string GetInvertSettingKey(CameraPatchDefinition patch)
    {
        return patch.Id + ".invert";
    }

    private static bool IsInverted(CameraPatchDefinition patch)
    {
        return RuntimeModSettings.TryGetSettingBool(GetInvertSettingKey(patch), out var overridden)
            ? overridden
            : patch.Settings.Invert;
    }

    private static float Quantize(float value, float step)
    {
        return step <= 0f ? value : (float)Math.Round(value / step) * step;
    }

    private static bool TryGetNumericValue(JsonElement element, out float value)
    {
        value = 0f;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetSingle(out value))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return float.TryParse(
                element.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        return false;
    }
}
