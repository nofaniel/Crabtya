using System.Reflection;
using System.Text.Json;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace EIC.ModLoader;

public static class CameraPatchApplicator
{
    private const string SupportedTarget = "camera.orthographicSize";
    private const float MinPerspectiveFieldOfView = 5f;
    private const float MaxPerspectiveFieldOfView = 170f;

    private static readonly string[] CandidateMethods =
    {
        "UnityEngine.Camera:set_orthographicSize",
        "UnityEngine.Camera:set_projectionMatrix",
        "General.CameraScaler:Update",
        "General.CameraScaler:AdjustOversizedCameraSize",
        "World.CombatCamera:LateUpdate"
    };

    private sealed class CameraBaseline
    {
        public bool HasOrthographicSize;
        public float OrthographicSize;
        public bool HasFieldOfView;
        public float FieldOfView;
    }

    private static readonly Dictionary<int, CameraBaseline> Baselines = new();
    private static readonly Dictionary<string, float> LastLoggedSizes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> LoggedPerspectiveFallback = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<Camera> CachedCameras = new();
    private static readonly List<CameraPatchDefinition> FrameSupportedPatches = new();
    private const float CameraRescanIntervalSeconds = 0.75f;
    private static ManualLogSource _log;
    private static int _suppressCameraSetterHooks;
    private static float _nextCameraRescanTime;
    private static int _supportedPatchCacheFrame = -1;

    public static void Install(ManualLogSource log)
    {
        _log = log;

        var harmony = new Harmony("eic.modloader.camerapatchapplicator");
        var installed = 0;

        foreach (var candidate in CandidateMethods)
        {
            var target = ResolveMethod(candidate);
            if (target is null)
            {
                log.LogWarning($"CameraPatchApplicator: hook candidate not found: {candidate}");
                continue;
            }

            var postfixMethod = ResolvePostfix(target);
            if (postfixMethod is null)
            {
                log.LogWarning(
                    $"CameraPatchApplicator: hook candidate has unsupported signature: {candidate} ({GetMethodSignature(target)})");
                continue;
            }

            try
            {
                var postfix = postfixMethod is null ? null : new HarmonyMethod(postfixMethod);
                harmony.Patch(target, postfix: postfix);
                installed++;
                log.LogInfo(
                    $"CameraPatchApplicator: hook installed: {candidate} ({GetMethodSignature(target)}), Postfix={postfixMethod?.Name ?? "<none>"}");
            }
            catch (Exception ex)
            {
                log.LogWarning(
                    $"CameraPatchApplicator: hook patch failed: {candidate} ({GetMethodSignature(target)}). Error={ex.Message}");
            }
        }

        log.LogInfo($"CameraPatchApplicator: hook installation complete. Installed={installed}, Candidates={CandidateMethods.Length}");
    }

    public static void ApplyToLiveCameras()
    {
        var patches = GetSupportedPatchesForCurrentFrame();
        if (patches.Count == 0)
        {
            return;
        }

        var cameras = GetLiveCameras();
        for (var i = 0; i < cameras.Count; i++)
        {
            TryApply(cameras[i], patches);
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
            // Light pruning between full scans in case a camera was destroyed.
            for (var i = CachedCameras.Count - 1; i >= 0; i--)
            {
                var camera = CachedCameras[i];
                var keep = false;
                try { keep = camera is not null && camera.isActiveAndEnabled && camera.gameObject.activeInHierarchy; }
                catch { /* IL2CPP destroyed object — treat as invalid */ }
                if (!keep) CachedCameras.RemoveAt(i);
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

        for (var i = 0; i < scanned.Length; i++)
        {
            var camera = scanned[i];
            var live = false;
            try { live = camera is not null && camera.isActiveAndEnabled && camera.gameObject.activeInHierarchy; }
            catch { /* IL2CPP destroyed object — skip */ }
            if (live) CachedCameras.Add(camera);
        }
    }

    private static MethodInfo ResolvePostfix(MethodInfo target)
    {
        if (!target.IsStatic)
        {
            return typeof(CameraPatchApplicator)
                .GetMethod(nameof(OnCameraSurfaceUpdatedFromInstance), BindingFlags.NonPublic | BindingFlags.Static);
        }

        var parameters = target.GetParameters();
        if (parameters.Length > 0 && typeof(Camera).IsAssignableFrom(parameters[0].ParameterType))
        {
            return typeof(CameraPatchApplicator)
                .GetMethod(nameof(OnCameraSurfaceUpdatedFromFirstCameraArgumentPostfix), BindingFlags.NonPublic | BindingFlags.Static);
        }

        return null;
    }

    private static void OnCameraSurfaceUpdatedFromInstance(object __instance, MethodBase __originalMethod)
    {
        if (_suppressCameraSetterHooks > 0 && __originalMethod is not null)
        {
            var name = __originalMethod.Name;
            if (name == "set_orthographicSize" || name == "set_projectionMatrix" || name == "set_fieldOfView")
            {
                return;
            }
        }

        TryApply(__instance, GetSupportedPatchesForCurrentFrame());
    }

    private static void OnCameraSurfaceUpdatedFromFirstCameraArgumentPostfix(Camera __0)
    {
        TryApply(__0, GetSupportedPatchesForCurrentFrame());
    }

    private static void TryApply(object source, IReadOnlyList<CameraPatchDefinition> patches)
    {
        if (source is null)
        {
            return;
        }

        var camera = ResolveCamera(source);
        if (camera is null)
        {
            return;
        }

        var key = camera.GetInstanceID();
        if (!Baselines.TryGetValue(key, out var baseline))
        {
            baseline = new CameraBaseline();
            Baselines[key] = baseline;
        }

        if (camera.orthographic)
        {
            if (!baseline.HasOrthographicSize)
            {
                baseline.OrthographicSize = camera.orthographicSize;
                baseline.HasOrthographicSize = true;
            }

            ApplyOrthographicPatches(source, camera, patches, baseline.OrthographicSize);
            return;
        }

        if (!baseline.HasFieldOfView)
        {
            baseline.FieldOfView = camera.fieldOfView;
            baseline.HasFieldOfView = true;
        }

        ApplyPerspectiveFallback(source, camera, patches, baseline.FieldOfView);
    }

    private static IReadOnlyList<CameraPatchDefinition> GetSupportedPatchesForCurrentFrame()
    {
        var frame = Time.frameCount;
        if (frame == _supportedPatchCacheFrame)
        {
            return FrameSupportedPatches;
        }

        _supportedPatchCacheFrame = frame;
        FrameSupportedPatches.Clear();
        foreach (var patch in RuntimeContentState.Current.CameraPatches.Values)
        {
            if (string.Equals(patch.Target, SupportedTarget, StringComparison.OrdinalIgnoreCase))
            {
                FrameSupportedPatches.Add(patch);
            }
        }

        return FrameSupportedPatches;
    }

    private static void ApplyOrthographicPatches(object source, Camera camera, IReadOnlyList<CameraPatchDefinition> patches, float baseline)
    {
        var updated = baseline;
        foreach (var patch in patches)
        {
            if (!TryGetPatchValue(patch, out var patchValue))
            {
                _log?.LogWarning($"CameraPatchApplicator: patch '{patch.Id}' has non-numeric value '{patch.GetValuePreview()}' - skipped.");
                continue;
            }

            updated = ApplyOp(patch.Operation, updated, patchValue);
        }

        if (updated <= 0f)
        {
            _log?.LogWarning($"CameraPatchApplicator: computed non-positive orthographic size {updated} - skipped.");
            return;
        }

        var before = camera.orthographicSize;
        if (Math.Abs(before - updated) < 0.001f)
        {
            return;
        }

        _suppressCameraSetterHooks++;
        try
        {
            camera.orthographicSize = updated;
            camera.ResetProjectionMatrix();
        }
        finally
        {
            _suppressCameraSetterHooks = Math.Max(0, _suppressCameraSetterHooks - 1);
        }

        TrySetMemberValue(source, updated, "_gameplayCameraOrthographicSize", "gameplayCameraOrthographicSize");

        var sourceName = source.GetType().FullName ?? source.GetType().Name;
        var patchSummary = string.Join(", ", patches.Select(p => $"{p.Id}={FormatPatchValue(p)}"));
        var logKey = $"{camera.GetInstanceID()}:{sourceName}:{camera.name}:ortho";
        if (!LastLoggedSizes.TryGetValue(logKey, out var lastLoggedSize) || Math.Abs(lastLoggedSize - updated) >= 0.049f)
        {
            LastLoggedSizes[logKey] = updated;
            _log?.LogInfo(
                $"CameraPatchApplicator: camera patch applied. Source={sourceName}, Camera={camera.name}, Mode=orthographic, Baseline={baseline}, Before={before}, After={updated}, Patches={patchSummary}");
        }
    }

    private static void ApplyPerspectiveFallback(object source, Camera camera, IReadOnlyList<CameraPatchDefinition> patches, float baselineFieldOfView)
    {
        var updatedFieldOfView = baselineFieldOfView;
        foreach (var patch in patches)
        {
            if (!TryGetPatchValue(patch, out var patchValue))
            {
                _log?.LogWarning($"CameraPatchApplicator: patch '{patch.Id}' has non-numeric value '{patch.GetValuePreview()}' - skipped.");
                continue;
            }

            updatedFieldOfView = ApplyOp(patch.Operation, updatedFieldOfView, patchValue);
        }

        updatedFieldOfView = Math.Clamp(updatedFieldOfView, MinPerspectiveFieldOfView, MaxPerspectiveFieldOfView);
        if (Math.Abs(camera.fieldOfView - updatedFieldOfView) < 0.001f)
        {
            return;
        }

        var before = camera.fieldOfView;
        _suppressCameraSetterHooks++;
        try
        {
            camera.fieldOfView = updatedFieldOfView;
            camera.ResetProjectionMatrix();
        }
        finally
        {
            _suppressCameraSetterHooks = Math.Max(0, _suppressCameraSetterHooks - 1);
        }

        var sourceName = source.GetType().FullName ?? source.GetType().Name;
        var patchSummary = string.Join(", ", patches.Select(p => $"{p.Id}={FormatPatchValue(p)}"));
        var fallbackKey = $"{camera.GetInstanceID()}:{camera.name}";
        if (LoggedPerspectiveFallback.Add(fallbackKey))
        {
            _log?.LogWarning(
                $"CameraPatchApplicator: using perspective fallback for '{camera.name}' because orthographic mode is false. Applying target '{SupportedTarget}' to Camera.fieldOfView.");
        }

        var logKey = $"{camera.GetInstanceID()}:{sourceName}:{camera.name}:perspective";
        if (!LastLoggedSizes.TryGetValue(logKey, out var lastLoggedSize) || Math.Abs(lastLoggedSize - updatedFieldOfView) >= 0.049f)
        {
            LastLoggedSizes[logKey] = updatedFieldOfView;
            _log?.LogInfo(
                $"CameraPatchApplicator: camera patch applied. Source={sourceName}, Camera={camera.name}, Mode=perspective-fallback, BaselineFov={baselineFieldOfView}, Before={before}, After={updatedFieldOfView}, Patches={patchSummary}");
        }
    }

    private static Camera ResolveCamera(object source)
    {
        if (source is Camera camera)
        {
            return camera;
        }

        var direct = TryGetMemberValue(source, "Camera", "_camera", "camera", "_gameplayCamera", "gameplayCamera");
        if (direct is Camera directCamera)
        {
            return directCamera;
        }

        var scaler = TryGetMemberValue(source, "CameraScaler", "_cameraScaler", "cameraScaler");
        if (scaler is not null && !ReferenceEquals(scaler, source))
        {
            var scalerCamera = TryGetMemberValue(scaler, "_gameplayCamera", "gameplayCamera", "Camera");
            if (scalerCamera is Camera nestedCamera)
            {
                return nestedCamera;
            }
        }

        return null;
    }

    private static object TryGetMemberValue(object source, params string[] names)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var type = source.GetType();
        foreach (var name in names)
        {
            try
            {
                var property = type.GetProperty(name, flags);
                if (property is not null && property.CanRead)
                {
                    return property.GetValue(source);
                }

                var field = type.GetField(name, flags);
                if (field is not null)
                {
                    return field.GetValue(source);
                }
            }
            catch
            {
                // IL2CPP reflection can reject some generated accessors; keep probing.
            }
        }

        return null;
    }

    private static void TrySetMemberValue(object source, object value, params string[] names)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var type = source.GetType();
        foreach (var name in names)
        {
            try
            {
                var property = type.GetProperty(name, flags);
                if (property is not null && property.CanWrite)
                {
                    property.SetValue(source, Convert.ChangeType(value, property.PropertyType));
                    return;
                }

                var field = type.GetField(name, flags);
                if (field is not null)
                {
                    field.SetValue(source, Convert.ChangeType(value, field.FieldType));
                    return;
                }
            }
            catch
            {
                // Best effort only; the Camera value itself is authoritative.
            }
        }
    }

    private static MethodInfo ResolveMethod(string candidate)
    {
        var separatorIndex = candidate.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= candidate.Length - 1)
        {
            return null;
        }

        var typeName = candidate[..separatorIndex];
        var methodName = candidate[(separatorIndex + 1)..];

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type;
            try { type = assembly.GetType(typeName, throwOnError: false, ignoreCase: false); }
            catch { continue; }
            if (type is null) continue;

            try
            {
                return type.GetMethod(
                    methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static string GetMethodSignature(MethodInfo method)
    {
        var parameters = string.Join(
            ", ",
            method.GetParameters().Select(p => p.ParameterType.FullName ?? p.ParameterType.Name));
        return $"{method.DeclaringType?.FullName}::{method.Name}({parameters})";
    }

    private static float ApplyOp(string op, float current, float patch)
    {
        return op.ToLowerInvariant() switch
        {
            "set" => patch,
            "add" => current + patch,
            "multiply" => current * patch,
            _ => current
        };
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
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
        }

        return false;
    }

    private static bool TryGetPatchValue(CameraPatchDefinition patch, out float value)
    {
        if ((patch.Settings.Slider || patch.Settings.MouseWheel) && RuntimeModSettings.TryGetFloat(patch.Id, out value))
        {
            return true;
        }

        return TryGetNumericValue(patch.Value, out value);
    }

    private static string FormatPatchValue(CameraPatchDefinition patch)
    {
        return TryGetPatchValue(patch, out var value)
            ? value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            : patch.GetValuePreview();
    }
}
