using System.Globalization;
using System.Text.Json;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EIC.ModLoader;

public static class UiScaleApplicator
{
    private const string SupportedTarget = "ui.scale";
    private const float TransitionBurstTickSeconds = 0.016f;
    private const float TransitionBurstDurationSeconds = 0.8f;
    private const float NonDefaultScaleTickSeconds = 0.05f;
    private const float DefaultScaleTickSeconds = 0.25f;
    private const float SelectionLogIntervalSeconds = 1.0f;
    private const float NonDefaultScaleEligibilityScanSeconds = 0.12f;
    private const float DefaultScaleEligibilityScanSeconds = 0.30f;
    private static readonly bool NativeUiScaleMutationEnabled = true;
    private static readonly string[] UiPathHints =
    {
        "screen",
        "menu",
        "hud",
        "panel",
        "button",
        "content",
        "bar",
        "icon",
        "slot",
        "skill",
        "ability",
        "resource",
        "counter",
        "queue",
        "progress",
        "health",
        "heart",
        "feedback",
        "credits",
        "roadmap",
        "discord",
        "settings",
        "quit",
        "codex",
        "challenge",
        "run"
    };

    private static readonly string[] BlockedScaleRootPathHints =
    {
        "BlackBars",
        "VisualPivot",
        "MainMenuScreen/Content/Logo",
        "MainMenuScreen/Logo",
        "DoomsdayClockParentForAnims",
        "KonamiCode"
    };

    private static readonly string[] DecorativePathHints =
    {
        "cloud",
        "logo",
        "background",
        "video",
        "vfx",
        "particle",
        "decoration",
        "ornament"
    };

    private sealed class ElementBaseline
    {
        public int InstanceId { get; init; }
        public RectTransform Transform { get; init; }
        public Vector3 LocalScale { get; init; }
    }

    private static readonly Dictionary<int, ElementBaseline> Baselines = new();
    private static readonly List<RectTransform> CachedEligibleRoots = new();
    private static ManualLogSource _log;
    private static float _lastLoggedScale = float.NaN;
    private static float _transitionBurstUntilSeconds;
    private static float _nextEligibleRootScanTime;
    private static float _nextSelectionLogTime;
    private static bool _loggedSelection;
    private static bool _loggedNoEligible;
    private static bool _loggedDisabled;
    private static string _lastSelectionFingerprint = string.Empty;

    public static bool IsNativeScaleMutationEnabled => NativeUiScaleMutationEnabled;

    public static float GetRecommendedUpdateIntervalSeconds()
    {
        if (!TryGetPrimaryDefinition(out var definition))
        {
            return DefaultScaleTickSeconds;
        }

        var targetScale = Mathf.Clamp(GetCurrentScale(definition), GetMin(definition), GetMax(definition));
        if (Math.Abs(targetScale - 1f) < 0.01f)
        {
            return DefaultScaleTickSeconds;
        }

        if (Time.unscaledTime < _transitionBurstUntilSeconds)
        {
            return TransitionBurstTickSeconds;
        }

        if (IsTransitionSensitivePopupActive())
        {
            return TransitionBurstTickSeconds;
        }

        return NonDefaultScaleTickSeconds;
    }

    public static Vector3 GetCapturedOrCurrentLocalScale(RectTransform transform)
    {
        if (transform is null)
        {
            return Vector3.one;
        }

        return TryGetBaseline(transform, out var baseline) ? baseline.LocalScale : transform.localScale;
    }

    public static void Install(ManualLogSource log)
    {
        _log = log;
        log.LogInfo("UiScaleApplicator: ready.");
    }

    public static void ApplyToLiveUi()
    {
        if (!TryGetPrimaryDefinition(out var definition))
        {
            RestoreAllTrackedRoots();
            return;
        }

        if (!NativeUiScaleMutationEnabled)
        {
            if (!_loggedDisabled)
            {
                _loggedDisabled = true;
                _log?.LogWarning("UiScaleApplicator: native UI scale mutation is disabled because transform scaling can corrupt game menus/gameplay viewport roots.");
            }

            RestoreAllTrackedRoots();

            return;
        }

        var targetScale = GetCurrentScale(definition);
        targetScale = Mathf.Clamp(targetScale, GetMin(definition), GetMax(definition));

        var eligible = GetEligibleRoots(targetScale);
        if (eligible.Count == 0)
        {
            if (!_loggedNoEligible)
            {
                _loggedNoEligible = true;
                _log?.LogInfo("UiScaleApplicator: no eligible interactive UI roots were discovered for scaling.");
            }

            if (!string.IsNullOrEmpty(_lastSelectionFingerprint))
            {
                _transitionBurstUntilSeconds = Math.Max(_transitionBurstUntilSeconds, Time.unscaledTime + TransitionBurstDurationSeconds);
            }

            PruneDroppedRoots(new HashSet<int>());

            return;
        }

        _loggedNoEligible = false;

        var currentRootIds = new HashSet<int>();
        for (var i = 0; i < eligible.Count; i++)
        {
            currentRootIds.Add(eligible[i].GetInstanceID());
        }

        PruneDroppedRoots(currentRootIds);

        var changed = 0;
        for (var i = 0; i < eligible.Count; i++)
        {
            var transform = eligible[i];
            var key = transform.GetInstanceID();
            if (!TryGetBaseline(transform, out var baseline))
            {
                baseline = CaptureBaseline(transform);
                Baselines[key] = baseline;
            }

            RestoreBaselinePose(transform, baseline);

            var elementScale = GetElementScale(transform, targetScale);
            var desired = baseline.LocalScale * elementScale;
            if (Vector3.Distance(transform.localScale, desired) < 0.001f)
            {
                continue;
            }

            transform.localScale = desired;
            changed++;
        }

        var selectionFingerprint = BuildSelectionFingerprint(eligible);
        if (!string.Equals(_lastSelectionFingerprint, selectionFingerprint, StringComparison.Ordinal))
        {
            _transitionBurstUntilSeconds = Time.unscaledTime + TransitionBurstDurationSeconds;
        }

        var fingerprintChanged = !string.Equals(_lastSelectionFingerprint, selectionFingerprint, StringComparison.Ordinal);
        if (!_loggedSelection || fingerprintChanged)
        {
            var shouldLog = !_loggedSelection || Time.unscaledTime >= _nextSelectionLogTime;
            _loggedSelection = true;
            _lastSelectionFingerprint = selectionFingerprint;
            if (shouldLog)
            {
                _nextSelectionLogTime = Time.unscaledTime + SelectionLogIntervalSeconds;
                var names = eligible.Select(DescribeElement).Take(12).ToList();
                var preview = names.Count == 0 ? "<none>" : string.Join(" | ", names);
                _log?.LogInfo($"UiScaleApplicator: bound roots={eligible.Count}. Roots={preview}");
            }
        }

        if (changed > 0 && (float.IsNaN(_lastLoggedScale) || Math.Abs(_lastLoggedScale - targetScale) >= 0.01f))
        {
            _lastLoggedScale = targetScale;
            _log?.LogInfo(
                $"UiScaleApplicator: applied scale {targetScale.ToString("0.##", CultureInfo.InvariantCulture)} to {changed}/{eligible.Count} interactive UI roots.");
        }
    }

    private static void RestoreAllTrackedRoots()
    {
        if (Baselines.Count > 0)
        {
            var ids = Baselines.Keys.ToList();
            for (var i = 0; i < ids.Count; i++)
            {
                RestoreBaselineById(ids[i]);
            }
        }

        Baselines.Clear();
        CachedEligibleRoots.Clear();
        _nextEligibleRootScanTime = 0f;
        _lastSelectionFingerprint = string.Empty;
        _loggedSelection = false;
        _nextSelectionLogTime = 0f;
        _lastLoggedScale = float.NaN;
        _transitionBurstUntilSeconds = 0f;
    }

    private static IReadOnlyList<RectTransform> GetEligibleRoots(float targetScale)
    {
        var shouldScanNow = Time.unscaledTime >= _nextEligibleRootScanTime || CachedEligibleRoots.Count == 0;
        if (!shouldScanNow)
        {
            // Remove dead roots from cache and force a rescan if everything dropped.
            for (var i = CachedEligibleRoots.Count - 1; i >= 0; i--)
            {
                var root = CachedEligibleRoots[i];
                if (root is null || root.gameObject is null || !root.gameObject.activeInHierarchy)
                {
                    CachedEligibleRoots.RemoveAt(i);
                }
            }

            shouldScanNow = CachedEligibleRoots.Count == 0;
        }

        if (shouldScanNow)
        {
            CachedEligibleRoots.Clear();
            CachedEligibleRoots.AddRange(CollectEligibleRoots());

            var scanInterval = GetEligibilityScanIntervalSeconds(targetScale);
            _nextEligibleRootScanTime = Time.unscaledTime + scanInterval;
        }

        return CachedEligibleRoots;
    }

    private static float GetEligibilityScanIntervalSeconds(float targetScale)
    {
        if (Time.unscaledTime < _transitionBurstUntilSeconds || IsTransitionSensitivePopupActive())
        {
            return TransitionBurstTickSeconds;
        }

        return Math.Abs(targetScale - 1f) < 0.01f
            ? DefaultScaleEligibilityScanSeconds
            : NonDefaultScaleEligibilityScanSeconds;
    }

    public static bool TryGetPrimaryDefinition(out UiScaleDefinition definition)
    {
        definition = RuntimeContentState.Current.UiScales.Values
            .Where(p => string.Equals(p.Target, SupportedTarget, StringComparison.OrdinalIgnoreCase))
            .LastOrDefault();
        return definition is not null;
    }

    public static float GetCurrentScale(UiScaleDefinition definition)
    {
        if (definition is not null && RuntimeModSettings.TryGetFloat(definition.Id, out var runtimeValue))
        {
            return runtimeValue;
        }

        return definition is not null && TryGetNumericValue(definition.Value, out var value) ? value : 1f;
    }

    public static float GetMin(UiScaleDefinition definition)
    {
        return definition?.Settings?.Min > 0f ? definition.Settings.Min : 0.5f;
    }

    public static float GetMax(UiScaleDefinition definition)
    {
        var min = GetMin(definition);
        return definition?.Settings?.Max > min ? definition.Settings.Max : min + 0.4f;
    }

    public static float GetStep(UiScaleDefinition definition)
    {
        return definition?.Settings?.Step > 0f ? definition.Settings.Step : 0.05f;
    }

    private static List<RectTransform> CollectEligibleRoots()
    {
        var list = new List<RectTransform>();
        var seen = new HashSet<int>();

        try
        {
            var selectables = Object.FindObjectsOfType<Selectable>();
            for (var i = 0; i < selectables.Length; i++)
            {
                var selectable = selectables[i];
                if (selectable is null || selectable.transform is not RectTransform rect)
                {
                    continue;
                }

                var root = FindInteractiveClusterRoot(rect);
                if (root is null)
                {
                    continue;
                }

                if (TryAddRoot(list, seen, root))
                {
                }
            }
        }
        catch
        {
            // continue with text-only probe
        }

        try
        {
            var texts = Object.FindObjectsOfType<Text>();
            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text is null || text.rectTransform is null)
                {
                    continue;
                }

                var rect = text.rectTransform;
                if (!ShouldConsiderStandaloneText(rect))
                {
                    continue;
                }

                if (IsUnderKnownRoot(rect, seen))
                {
                    continue;
                }

                var root = FindStandaloneTextRoot(rect);
                if (root is null)
                {
                    continue;
                }

                if (TryAddRoot(list, seen, root))
                {
                }
            }
        }
        catch
        {
            // best-effort
        }

        try
        {
            var tmpTexts = Object.FindObjectsOfType<TMP_Text>();
            for (var i = 0; i < tmpTexts.Length; i++)
            {
                var text = tmpTexts[i];
                if (text is null || text.rectTransform is null)
                {
                    continue;
                }

                var rect = text.rectTransform;
                if (!ShouldConsiderStandaloneText(rect))
                {
                    continue;
                }

                if (IsUnderKnownRoot(rect, seen))
                {
                    continue;
                }

                var root = FindStandaloneTextRoot(rect);
                if (root is null)
                {
                    continue;
                }

                if (TryAddRoot(list, seen, root))
                {
                }
            }
        }
        catch
        {
            // best-effort
        }

        try
        {
            var graphics = Object.FindObjectsOfType<Graphic>();
            for (var i = 0; i < graphics.Length; i++)
            {
                var graphic = graphics[i];
                if (graphic is null || graphic.rectTransform is null)
                {
                    continue;
                }

                var rect = graphic.rectTransform;
                if (!ShouldConsiderPresentationGraphic(rect, graphic))
                {
                    continue;
                }

                if (IsUnderKnownRoot(rect, seen))
                {
                    continue;
                }

                var root = FindGraphicClusterRoot(rect);
                if (root is null)
                {
                    continue;
                }

                if (TryAddRoot(list, seen, root))
                {
                }
            }
        }
        catch
        {
            // best-effort
        }

        return list;
    }

    private static RectTransform FindInteractiveClusterRoot(RectTransform element)
    {
        if (!CommonEligibilityChecks(element))
        {
            return null;
        }

        return element;
    }

    private static RectTransform FindStandaloneTextRoot(RectTransform element)
    {
        if (!ShouldConsiderStandaloneText(element))
        {
            return null;
        }

        return element;
    }

    private static RectTransform FindGraphicClusterRoot(RectTransform element)
    {
        if (!CommonEligibilityChecks(element))
        {
            return null;
        }

        return element;
    }

    private static bool ShouldConsiderStandaloneText(RectTransform element)
    {
        if (!CommonEligibilityChecks(element))
        {
            return false;
        }

        var path = BuildTransformPath(element.transform);
        if (IsUnsafeScaleRootPath(path))
        {
            return false;
        }

        if (element.GetComponentInParent<Selectable>() is not null)
        {
            return false;
        }

        if (LooksLikeFullscreenElement(element) || IsOversizedCluster(element, 0.30f))
        {
            return false;
        }

        return true;
    }

    private static bool ShouldConsiderGraphicSeed(RectTransform element, Graphic graphic)
    {
        if (!CommonEligibilityChecks(element))
        {
            return false;
        }

        var area = Math.Abs(element.rect.width * element.rect.height);
        if (area < 4f)
        {
            return false;
        }

        if (LooksLikeFullscreenElement(element) || IsOversizedCluster(element, 0.25f))
        {
            return false;
        }

        var path = BuildTransformPath(element.transform);
        if (path.Contains("Video", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if ((path.Contains("Logo", StringComparison.OrdinalIgnoreCase) ||
             path.Contains("Background", StringComparison.OrdinalIgnoreCase)) &&
            area > Math.Max(1f, Screen.width * Screen.height) * 0.05f)
        {
            return false;
        }

        return true;
    }

    private static bool ShouldConsiderPresentationGraphic(RectTransform element, Graphic graphic)
    {
        if (!ShouldConsiderGraphicSeed(element, graphic))
        {
            return false;
        }

        var path = BuildTransformPath(element.transform);
        return IsScaleDownOnlyPresentationPath(path);
    }

    private static bool TryAddRoot(List<RectTransform> list, HashSet<int> seen, RectTransform root)
    {
        if (root is null)
        {
            return false;
        }

        if (!IsLikelyUiSurfaceRoot(root))
        {
            return false;
        }

        for (var i = 0; i < list.Count; i++)
        {
            var existing = list[i];
            if (existing is null)
            {
                continue;
            }

            if (IsSameTransform(existing, root) || IsAncestorOrSelf(existing, root))
            {
                return false;
            }
        }

        var removedAny = false;
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var existing = list[i];
            if (existing is null)
            {
                continue;
            }

            if (!IsAncestorOrSelf(root, existing))
            {
                continue;
            }

            RestoreBaselineById(existing.GetInstanceID());
            seen.Remove(existing.GetInstanceID());
            Baselines.Remove(existing.GetInstanceID());
            list.RemoveAt(i);
            removedAny = true;
        }

        if (removedAny)
        {
            // allow root to replace its descendant subtree selections cleanly.
            seen.Remove(root.GetInstanceID());
        }

        if (!seen.Add(root.GetInstanceID()))
        {
            return false;
        }

        list.Add(root);
        return true;
    }

    private static bool IsSameTransform(RectTransform a, RectTransform b)
    {
        return a is not null && b is not null && a.GetInstanceID() == b.GetInstanceID();
    }

    private static bool IsAncestorOrSelf(RectTransform ancestor, RectTransform descendant)
    {
        if (ancestor is null || descendant is null)
        {
            return false;
        }

        var cursor = descendant;
        while (cursor is not null)
        {
            if (cursor.GetInstanceID() == ancestor.GetInstanceID())
            {
                return true;
            }

            cursor = cursor.parent as RectTransform;
        }

        return false;
    }

    private static bool IsLikelyUiSurfaceRoot(RectTransform rect)
    {
        if (!CommonEligibilityChecks(rect))
        {
            return false;
        }

        var path = BuildTransformPath(rect.transform);
        if (IsUnsafeScaleRootPath(path) || IsDecorativePath(path))
        {
            return false;
        }

        var selectableCount = SafeCountComponents<Selectable>(rect);
        var textCount = SafeCountComponents<Text>(rect);
        var tmpCount = SafeCountComponents<TMP_Text>(rect);
        var graphicCount = SafeCountComponents<Graphic>(rect);
        if (selectableCount > 0 || textCount > 0 || tmpCount > 0)
        {
            return true;
        }

        if (ContainsAnyHint(path, UiPathHints) && graphicCount >= 2)
        {
            return true;
        }

        return false;
    }

    private static bool IsUnderKnownRoot(RectTransform element, HashSet<int> roots)
    {
        var cursor = element;
        while (cursor is not null)
        {
            if (roots.Contains(cursor.GetInstanceID()))
            {
                return true;
            }

            cursor = cursor.parent as RectTransform;
        }

        return false;
    }

    private static bool CommonEligibilityChecks(RectTransform element)
    {
        if (element is null || element.gameObject is null)
        {
            return false;
        }

        var path = BuildTransformPath(element.transform);
        if (path.Contains("EIC.ModLoader", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("Cursor", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("Quantum Console", StringComparison.OrdinalIgnoreCase) ||
            IsUnsafeScaleRootPath(path) ||
            IsDecorativePath(path))
        {
            return false;
        }

        if (path.Contains("Crabtya", StringComparison.OrdinalIgnoreCase) &&
            !IsCrabtyaInjectedNativeButtonPath(path))
        {
            return false;
        }

        var canvas = element.GetComponentInParent<Canvas>();
        if (canvas is null || canvas.renderMode == RenderMode.WorldSpace)
        {
            return false;
        }

        return true;
    }

    private static bool IsUnsafeScaleRootPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        if (path.EndsWith("/Canvas", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("Canvas", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/MainMenuScreen", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/MainMenuScreen/Content", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (var i = 0; i < BlockedScaleRootPathHints.Length; i++)
        {
            if (path.EndsWith("/" + BlockedScaleRootPathHints[i], StringComparison.OrdinalIgnoreCase) ||
                path.Equals(BlockedScaleRootPathHints[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsScaleDownOnlyPresentationPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsCrabtyaInjectedNativeButtonPath(path))
        {
            return false;
        }

        return path.Contains("LevelSelect", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("SelectivePressure", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("Selective Pressure", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("Pressure", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("Progression", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("Reward", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("Rewards", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCrabtyaInjectedNativeButtonPath(string path)
    {
        return path.Contains("Crabtya.MainMenu.ModsButton", StringComparison.Ordinal) ||
               path.Contains("Crabtya.PauseMenu.ModsButton", StringComparison.Ordinal);
    }

    private static float GetElementScale(RectTransform transform, float targetScale)
    {
        if (targetScale <= 1f)
        {
            return targetScale;
        }

        var path = BuildTransformPath(transform.transform);
        if (IsScaleDownOnlyPresentationPath(path))
        {
            return 1f;
        }

        var screenArea = Math.Max(1f, Screen.width * Screen.height);
        var elementArea = Math.Abs(transform.rect.width * transform.rect.height);
        var areaRatio = elementArea / screenArea;
        if (areaRatio >= 0.05f)
        {
            return Math.Min(targetScale, 1.05f);
        }

        if (areaRatio >= 0.025f)
        {
            return Math.Min(targetScale, 1.1f);
        }

        return targetScale;
    }

    private static string BuildSelectionFingerprint(IReadOnlyList<RectTransform> roots)
    {
        return string.Join("|", roots.Select(DescribeElement).OrderBy(p => p, StringComparer.Ordinal));
    }

    private static bool IsTransitionSensitivePopupActive()
    {
        try
        {
            var popup = GameObject.Find("GenericDecisionPopup");
            return popup is not null && popup.activeInHierarchy;
        }
        catch
        {
            return false;
        }
    }

    private static bool LooksLikeFullscreenElement(RectTransform rect)
    {
        if (rect is null)
        {
            return false;
        }

        var screenArea = Math.Max(1f, Screen.width * Screen.height);
        var area = Math.Abs(rect.rect.width * rect.rect.height);
        if (area < screenArea * 0.55f)
        {
            return false;
        }

        if (rect.anchorMin.x <= 0.01f &&
            rect.anchorMin.y <= 0.01f &&
            rect.anchorMax.x >= 0.99f &&
            rect.anchorMax.y >= 0.99f)
        {
            return true;
        }

        var graphic = rect.GetComponent<Graphic>();
        return graphic is RawImage || graphic is Image;
    }

    private static bool IsOversizedCluster(RectTransform rect, float threshold)
    {
        var screenArea = Math.Max(1f, Screen.width * Screen.height);
        var area = Math.Abs(rect.rect.width * rect.rect.height);
        return area > screenArea * threshold;
    }

    private static string DescribeElement(RectTransform element)
    {
        return BuildTransformPath(element.transform);
    }

    private static int SafeCountComponents<T>(RectTransform rect) where T : Component
    {
        try
        {
            return rect.GetComponentsInChildren<T>(true).Length;
        }
        catch
        {
            return 0;
        }
    }

    private static bool IsDecorativePath(string path)
    {
        return ContainsAnyHint(path, DecorativePathHints);
    }

    private static bool ContainsAnyHint(string value, IReadOnlyList<string> hints)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        for (var i = 0; i < hints.Count; i++)
        {
            if (value.Contains(hints[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildTransformPath(Transform transform)
    {
        if (transform is null)
        {
            return string.Empty;
        }

        var names = new List<string>();
        var cursor = transform;
        while (cursor is not null)
        {
            names.Add(cursor.name);
            cursor = cursor.parent;
        }

        names.Reverse();
        return string.Join("/", names);
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

    private static bool TryGetBaseline(RectTransform transform, out ElementBaseline baseline)
    {
        baseline = null;
        if (transform is null)
        {
            return false;
        }

        var key = transform.GetInstanceID();
        if (!Baselines.TryGetValue(key, out var candidate))
        {
            return false;
        }

        if (candidate is null ||
            candidate.InstanceId != key ||
            candidate.Transform is null ||
            candidate.Transform.GetInstanceID() != key)
        {
            Baselines.Remove(key);
            return false;
        }

        baseline = candidate;
        return true;
    }

    private static ElementBaseline CaptureBaseline(RectTransform transform)
    {
        return new ElementBaseline
        {
            InstanceId = transform.GetInstanceID(),
            Transform = transform,
            LocalScale = transform.localScale
        };
    }

    private static void RestoreBaselinePose(RectTransform transform, ElementBaseline baseline)
    {
        if (transform is null || baseline is null)
        {
            return;
        }

        transform.localScale = baseline.LocalScale;
    }

    private static void RestoreBaselineById(int id)
    {
        if (!Baselines.TryGetValue(id, out var baseline))
        {
            return;
        }

        var transform = baseline?.Transform;
        if (transform is null || transform.GetInstanceID() != id)
        {
            Baselines.Remove(id);
            return;
        }

        RestoreBaselinePose(transform, baseline);
    }

    private static void PruneDroppedRoots(HashSet<int> currentRootIds)
    {
        if (Baselines.Count == 0)
        {
            return;
        }

        var staleIds = new List<int>();
        var trackedIds = Baselines.Keys.ToList();
        for (var i = 0; i < trackedIds.Count; i++)
        {
            var id = trackedIds[i];
            if (currentRootIds.Contains(id))
            {
                continue;
            }

            if (!Baselines.TryGetValue(id, out var baseline) ||
                baseline is null ||
                baseline.Transform is null ||
                baseline.Transform.GetInstanceID() != id)
            {
                staleIds.Add(id);
            }
        }

        for (var i = 0; i < staleIds.Count; i++)
        {
            Baselines.Remove(staleIds[i]);
        }
    }
}
