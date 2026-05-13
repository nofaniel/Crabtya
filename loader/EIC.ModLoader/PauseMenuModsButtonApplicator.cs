using TMPro;
using UnityEngine;
using UnityEngine.UI;
using World.UI;

namespace EIC.ModLoader;

/// <summary>
/// Injects a Crabtya "MOD SETTINGS" entry into the in-run pause/start menu.
/// The cloned button opens the same dedicated <see cref="CrabtyaModsSettingsWindow"/>
/// surface as the main-menu MODS button, keeping the settings flow single-surface.
/// </summary>
public static class PauseMenuModsButtonApplicator
{
    private const string CrabtyaButtonName = "Crabtya.PauseMenu.ModsButton";
    private const string ClickInterceptorName = "Crabtya.ClickInterceptor";
    private const string ModsLabel = "MOD SETTINGS";
    private const string SideModsLabel = "MODS";

    private static GameObject? _buttonObject;
    private static Transform? _buttonParent;
    private static PauseScreenUI? _pauseScreen;
    private static float _nextProbe;
    private static bool _loggedCreation;
    private static bool _loggedMissingPauseMenu;
    private static bool _buttonPreviouslyPresent;
    private static RectTransform? _shiftedRect;
    private static Vector2 _shiftedOriginalPosition;

    public static void Update()
    {
        RefreshButtonStateForRetryLogging();

        if (Time.unscaledTime < _nextProbe)
        {
            return;
        }

        _nextProbe = Time.unscaledTime + 0.2f;

        if (!TryGetActivePauseScreen(out var pauseScreen))
        {
            if (!_loggedMissingPauseMenu && _buttonPreviouslyPresent)
            {
                _loggedMissingPauseMenu = true;
                Plugin.Instance?.Log.LogInfo("Crabtya pause-menu probe: no active PauseScreenUI found; clearing prior Mods entry.");
            }

            DestroyNativeButton();
            return;
        }

        _loggedMissingPauseMenu = false;

        if (!TryFindMainButtonStack(pauseScreen, out var stackParent, out var templateButton))
        {
            return;
        }

        var existing = FindExistingButton(stackParent);
        if (existing is not null)
        {
            _buttonObject = existing;
            _buttonParent = stackParent;
            _pauseScreen = pauseScreen;
            _buttonPreviouslyPresent = true;
            StripLocalizationComponents(existing);
            TrySetButtonLabel(existing, ModsLabel);
            EnsureClickHandler(existing);
            return;
        }

        // The side button is parented to the background panel, not stackParent, so
        // FindExistingButton won't find it there.  If our reference is still valid,
        // just refresh the click handler and bail out to avoid recreating it.
        if (_buttonObject != null && (UnityEngine.Object)_buttonObject != null)
        {
            _pauseScreen = pauseScreen;
            EnsureClickHandler(_buttonObject);
            return;
        }

        try
        {
            if (!CreateButtonFromTemplate(pauseScreen, templateButton, stackParent))
            {
                Plugin.Instance?.Log.LogWarning("Crabtya pause-menu Mods button creation attempt did not produce a native clone.");
            }
        }
        catch (Exception ex)
        {
            Plugin.Instance?.Log.LogWarning($"Crabtya pause-menu Mods button creation failed: {ex}");
        }
    }

    private static void RefreshButtonStateForRetryLogging()
    {
        if (_buttonObject != null)
        {
            _buttonPreviouslyPresent = true;
            return;
        }

        if (!_buttonPreviouslyPresent)
        {
            return;
        }

        _buttonPreviouslyPresent = false;
        _buttonParent = null;
        _pauseScreen = null;
        _loggedCreation = false;
    }

    private static bool TryGetActivePauseScreen(out PauseScreenUI pauseScreen)
    {
        pauseScreen = null!;

        PauseScreenUI[] candidates;
        try
        {
            candidates = Resources.FindObjectsOfTypeAll<PauseScreenUI>();
        }
        catch
        {
            return false;
        }

        for (var i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];
            if (candidate is null || candidate.gameObject is null)
            {
                continue;
            }

            if (!candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            pauseScreen = candidate;
            return true;
        }

        return false;
    }

    private static bool TryFindMainButtonStack(PauseScreenUI pauseScreen, out Transform stackParent, out Button_ODD templateButton)
    {
        stackParent = null!;
        templateButton = null!;

        Button_ODD[] buttons;
        try
        {
            buttons = pauseScreen.GetComponentsInChildren<Button_ODD>(true);
        }
        catch
        {
            return false;
        }

        if (buttons.Length == 0)
        {
            return false;
        }

        // Group buttons by parent and find the parent containing the most Button_ODD children.
        // That is the main vertical stack (Resume/Character/Restart/Settings/Quit-to-menu).
        var counts = new Dictionary<Transform, List<Button_ODD>>();
        for (var i = 0; i < buttons.Length; i++)
        {
            var btn = buttons[i];
            if (btn is null || btn.gameObject is null)
            {
                continue;
            }

            if (IsCrabtyaObject(btn.transform))
            {
                continue;
            }

            if (!btn.gameObject.activeInHierarchy)
            {
                continue;
            }

            var parent = btn.transform.parent;
            if (parent is null)
            {
                continue;
            }

            if (!counts.TryGetValue(parent, out var list))
            {
                list = new List<Button_ODD>();
                counts[parent] = list;
            }

            list.Add(btn);
        }

        Transform? bestParent = null;
        List<Button_ODD>? bestList = null;
        foreach (var pair in counts)
        {
            if (pair.Value.Count < 2)
            {
                continue;
            }

            if (bestList is null || pair.Value.Count > bestList.Count)
            {
                bestParent = pair.Key;
                bestList = pair.Value;
            }
        }

        if (bestParent is null || bestList is null || bestList.Count < 2)
        {
            return false;
        }

        // Pick a template that has a RectTransform; pick top-most (highest sibling y).
        Button_ODD? best = null;
        var bestY = float.NegativeInfinity;
        for (var i = 0; i < bestList.Count; i++)
        {
            var candidate = bestList[i];
            var rect = candidate.GetComponent<RectTransform>();
            if (rect is null)
            {
                continue;
            }

            if (best is null || rect.anchoredPosition.y > bestY)
            {
                best = candidate;
                bestY = rect.anchoredPosition.y;
            }
        }

        if (best is null)
        {
            return false;
        }

        stackParent = bestParent;
        templateButton = best;
        return true;
    }

    private static bool CreateButtonFromTemplate(PauseScreenUI pauseScreen, Button_ODD templateButton, Transform parent)
    {
        DestroyNativeButton();

        var templateRect = templateButton.GetComponent<RectTransform>();
        if (templateRect is null)
        {
            return false;
        }

        // --- Side-button approach ---
        // Read the stack's world corners BEFORE creating any clone so the layout is
        // unaffected.  Use GetComponent<RectTransform>() instead of a direct cast
        // because the IL2CPP implicit cast operator is unavailable and throws.
        var stackRect = parent.GetComponent<RectTransform>();
        var pauseScreenRect = pauseScreen.GetComponent<RectTransform>();

        if (stackRect is not null && pauseScreenRect is not null)
        {
            // GetWorldCorners(Vector3[]) silently returns zeros in BepInEx IL2CPP because
            // it receives a managed array instead of an IL2CPP-native array.
            // Use TransformPoint on local rect corners instead — no array interop needed.
            var localRightCenter = new Vector3(stackRect.rect.xMax, stackRect.rect.center.y, 0f);
            var worldRightCenter = stackRect.TransformPoint(localRightCenter);
            var screenPoint = new Vector2(worldRightCenter.x, worldRightCenter.y);

            // Clone directly into pauseScreenRect — never instantiated into the stack.
            var clone = UnityEngine.Object.Instantiate(templateButton.gameObject, pauseScreenRect);
            clone.name = CrabtyaButtonName;
            clone.SetActive(true);

            var cloneRect = clone.GetComponent<RectTransform>();
            if (cloneRect is null)
            {
                UnityEngine.Object.Destroy(clone);
                return false;
            }

            // Convert screen-space right-center of the stack to PauseScreen local space.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                pauseScreenRect, screenPoint, null, out var localPoint);

            var nativeHeight = templateRect.sizeDelta.y > 0f ? templateRect.sizeDelta.y : 60f;
            cloneRect.anchorMin = new Vector2(0.5f, 0.5f);
            cloneRect.anchorMax = new Vector2(0.5f, 0.5f);
            cloneRect.pivot = new Vector2(0.5f, 0.5f);
            cloneRect.sizeDelta = new Vector2(130f, nativeHeight);
            // Button center = right edge of stack + 40 px gap + half button width (65 px).
            cloneRect.anchoredPosition = new Vector2(localPoint.x + 40f + 65f, localPoint.y);
            cloneRect.localScale = UiScaleApplicator.GetCapturedOrCurrentLocalScale(templateRect);
            cloneRect.localRotation = Quaternion.identity;

            StripLocalizationComponents(clone);
            StripNonUnityNonNativeComponents(clone);
            TrySetButtonLabel(clone, SideModsLabel);
            InstallClickInterceptor(clone);

            _buttonObject = clone;
            _buttonParent = pauseScreenRect;
            _pauseScreen = pauseScreen;
            _buttonPreviouslyPresent = true;
            _shiftedRect = null;

            if (!_loggedCreation)
            {
                _loggedCreation = true;
                Plugin.Instance?.Log.LogInfo(
                    $"Crabtya pause-menu Mods button created as side button. Parent='{BuildPath(pauseScreenRect)}', AnchoredPosition={cloneRect.anchoredPosition}, ScreenPoint={screenPoint}, LocalPoint={localPoint}");
            }

            return true;
        }

        // --- Fallback: insert into the stack ---
        // No RectTransform available; insert without shifting or expanding so we at
        // least get a button even if layout is imperfect.
        var fallbackClone = UnityEngine.Object.Instantiate(templateButton.gameObject, parent);
        fallbackClone.name = CrabtyaButtonName;
        fallbackClone.SetActive(true);

        var fallbackCloneRect = fallbackClone.GetComponent<RectTransform>();
        if (fallbackCloneRect is null)
        {
            UnityEngine.Object.Destroy(fallbackClone);
            return false;
        }

        fallbackCloneRect.anchorMin = templateRect.anchorMin;
        fallbackCloneRect.anchorMax = templateRect.anchorMax;
        fallbackCloneRect.pivot = templateRect.pivot;
        fallbackCloneRect.sizeDelta = templateRect.sizeDelta;
        fallbackCloneRect.localScale = UiScaleApplicator.GetCapturedOrCurrentLocalScale(templateRect);
        fallbackCloneRect.localRotation = templateRect.localRotation;

        var hasLayoutGroup = parent.GetComponent<LayoutGroup>() is not null;
        var (siblingIndex, position, shiftedRect, shiftedOriginal, _) = ChooseInsertionSlot(parent, templateRect, hasLayoutGroup);
        fallbackClone.transform.SetSiblingIndex(siblingIndex);
        if (!hasLayoutGroup)
        {
            fallbackCloneRect.anchoredPosition = position;
        }

        _shiftedRect = shiftedRect;
        _shiftedOriginalPosition = shiftedOriginal;

        StripLocalizationComponents(fallbackClone);
        StripNonUnityNonNativeComponents(fallbackClone);

        if (!TrySetButtonLabel(fallbackClone, ModsLabel))
        {
            Plugin.Instance?.Log.LogWarning($"Crabtya pause-menu Mods button created but no supported text component was found on '{fallbackClone.name}'.");
        }

        InstallClickInterceptor(fallbackClone);

        _buttonObject = fallbackClone;
        _buttonParent = parent;
        _pauseScreen = pauseScreen;
        _buttonPreviouslyPresent = true;

        if (!_loggedCreation)
        {
            _loggedCreation = true;
            Plugin.Instance?.Log.LogInfo(
                $"Crabtya pause-menu Mods button created (fallback stack insertion). Parent='{BuildPath(parent)}', SiblingIndex={siblingIndex}, HasLayoutGroup={hasLayoutGroup}, AnchoredPosition={fallbackCloneRect.anchoredPosition}");
        }

        return true;
    }

    private static RectTransform? FindBackgroundPanel(Transform start)
    {
        var current = start;
        while (current is not null)
        {
            var img = current.GetComponent<Image>();
            // If we found an image that has a sliced or tiled type, it's highly likely the panel background.
            // Or if it's named something like "Panel" or "Background".
            if (img is not null && img.enabled)
            {
                var name = current.name ?? string.Empty;
                if (img.type == Image.Type.Sliced || img.type == Image.Type.Tiled || 
                    name.Contains("Panel", StringComparison.OrdinalIgnoreCase) || 
                    name.Contains("Background", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Board", StringComparison.OrdinalIgnoreCase))
                {
                    return current.GetComponent<RectTransform>();
                }
            }
            current = current.parent;
        }

        // Fallback: just return the parent if it has an image
        current = start;
        while (current is not null)
        {
            var img = current.GetComponent<Image>();
            if (img is not null && img.enabled)
            {
                return current.GetComponent<RectTransform>();
            }
            current = current.parent;
        }

        return null;
    }

    private static (int siblingIndex, Vector2 position, RectTransform? shiftedRect, Vector2 shiftedOriginal, float stepAmount) ChooseInsertionSlot(Transform parent, RectTransform templateRect, bool hasLayoutGroup)
    {
        var nativeRects = new List<RectTransform>();
        for (var i = 0; i < parent.childCount; i++)
        {
            if (parent.GetChild(i) is RectTransform rect &&
                !IsCrabtyaObject(rect))
            {
                if (rect.GetComponent<Button_ODD>() is not null ||
                    rect.GetComponent<Button>() is not null)
                {
                    nativeRects.Add(rect);
                }
            }
        }

        if (nativeRects.Count == 0)
        {
            return (parent.childCount, templateRect.anchoredPosition, null, Vector2.zero, 0f);
        }

        // Place BEFORE the last button (between SETTINGS and QUIT TO MENU).
        // This keeps it inside the panel bounds like the main-menu approach.
        var ordered = nativeRects.OrderBy(r => r.transform.GetSiblingIndex()).ToList();
        var lastButton = ordered[ordered.Count - 1];

        // Insert at the sibling index of the last button (pushing it down by one).
        var siblingIndex = lastButton.transform.GetSiblingIndex();

        if (hasLayoutGroup)
        {
            // If it has a layout group, the background might auto-expand or might need manual expansion.
            // Let's estimate the step amount anyway.
            var yVals = ordered.Select(r => r.anchoredPosition.y).ToList();
            var spc = EstimateSpacing(yVals, 64f);
            return (siblingIndex, templateRect.anchoredPosition, null, Vector2.zero, spc);
        }

        // Position: take the last button's current position; shift that button down
        // by one spacing unit, and use its original position for MOD SETTINGS.
        var yByIndex = ordered.Select(r => r.anchoredPosition.y).ToList();
        var spacing = EstimateSpacing(yByIndex, 64f);
        var direction = EstimateVerticalFlowDirection(ordered);
        var step = direction < 0 ? -spacing : spacing;

        // Our button takes the position of the last button; the last button shifts down.
        var position = lastButton.anchoredPosition;
        var originalLastPos = lastButton.anchoredPosition;
        lastButton.anchoredPosition = new Vector2(lastButton.anchoredPosition.x, lastButton.anchoredPosition.y + step);

        return (siblingIndex, position, lastButton, originalLastPos, spacing);
    }

    private static float EstimateSpacing(IReadOnlyList<float> yValues, float fallback)
    {
        if (yValues.Count < 2)
        {
            return fallback;
        }

        var ordered = yValues.Distinct().OrderByDescending(v => v).ToList();
        var deltas = new List<float>();
        for (var i = 1; i < ordered.Count; i++)
        {
            var delta = Math.Abs(ordered[i - 1] - ordered[i]);
            if (delta > 8f && delta < 260f)
            {
                deltas.Add(delta);
            }
        }

        if (deltas.Count == 0)
        {
            return fallback;
        }

        deltas.Sort();
        return deltas[deltas.Count / 2];
    }

    private static int EstimateVerticalFlowDirection(IReadOnlyList<RectTransform> rects)
    {
        var ordered = rects.OrderBy(r => r.transform.GetSiblingIndex()).ToList();
        var deltas = new List<float>();
        for (var i = 1; i < ordered.Count; i++)
        {
            deltas.Add(ordered[i].anchoredPosition.y - ordered[i - 1].anchoredPosition.y);
        }

        if (deltas.Count == 0)
        {
            return -1;
        }

        var descending = deltas.Count(d => d < 0f);
        var ascending = deltas.Count(d => d > 0f);
        if (descending == ascending)
        {
            return -1;
        }

        return descending > ascending ? -1 : 1;
    }

    private static GameObject? FindExistingButton(Transform parent)
    {
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (string.Equals(child.name, CrabtyaButtonName, StringComparison.Ordinal))
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static void EnsureClickHandler(GameObject buttonObject)
    {
        InstallClickInterceptor(buttonObject);
    }

    private static void InstallClickInterceptor(GameObject buttonObject)
    {
        DisableNativeButtonRaycasts(buttonObject);

        Transform? existing = null;
        for (var i = 0; i < buttonObject.transform.childCount; i++)
        {
            var child = buttonObject.transform.GetChild(i);
            if (string.Equals(child.name, ClickInterceptorName, StringComparison.Ordinal))
            {
                existing = child;
                break;
            }
        }

        GameObject overlay;
        if (existing is not null)
        {
            overlay = existing.gameObject;
        }
        else
        {
            overlay = new GameObject(ClickInterceptorName);
            overlay.transform.SetParent(buttonObject.transform, false);
            var rect = overlay.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = overlay.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            overlay.AddComponent<Button>();
        }

        overlay.transform.SetAsLastSibling();

        var overlayButton = overlay.GetComponent<Button>();
        if (overlayButton is not null)
        {
            overlayButton.onClick.RemoveAllListeners();
            overlayButton.onClick.AddListener((Action)CrabtyaModsSettingsWindow.Show);
        }
    }

    private static void DisableNativeButtonRaycasts(GameObject buttonObject)
    {
        var images = buttonObject.GetComponentsInChildren<Image>(true);
        for (var i = 0; i < images.Length; i++)
        {
            var image = images[i];
            if (image is null)
            {
                continue;
            }

            var owner = image.gameObject;
            if (owner is null || string.Equals(owner.name, ClickInterceptorName, StringComparison.Ordinal))
            {
                continue;
            }

            image.raycastTarget = false;
        }

        var texts = buttonObject.GetComponentsInChildren<Text>(true);
        for (var i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            if (text is null)
            {
                continue;
            }

            text.raycastTarget = false;
        }
    }

    private static bool TrySetButtonLabel(GameObject buttonObject, string label)
    {
        var changed = false;

        var uiTexts = buttonObject.GetComponentsInChildren<Text>(true);
        for (var i = 0; i < uiTexts.Length; i++)
        {
            var uiText = uiTexts[i];
            if (uiText is null)
            {
                continue;
            }

            uiText.text = label;
            changed = true;
        }

        var tmpTexts = buttonObject.GetComponentsInChildren<TMP_Text>(true);
        for (var i = 0; i < tmpTexts.Length; i++)
        {
            var tmpText = tmpTexts[i];
            if (tmpText is null)
            {
                continue;
            }

            tmpText.text = label;
            changed = true;
        }

        return changed;
    }

    private static int StripLocalizationComponents(GameObject buttonObject)
    {
        var stripped = 0;
        Component[] components;
        try
        {
            components = buttonObject.GetComponentsInChildren<Component>(true);
        }
        catch
        {
            return 0;
        }

        for (var i = 0; i < components.Length; i++)
        {
            var component = components[i];
            if (component is null)
            {
                continue;
            }

            var typeName = component.GetType().FullName ?? string.Empty;
            if (!IsLocalizationComponentType(typeName))
            {
                continue;
            }

            try
            {
                UnityEngine.Object.Destroy(component);
                stripped++;
            }
            catch
            {
            }
        }

        return stripped;
    }

    private static bool IsLocalizationComponentType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return false;
        }

        return typeName.Contains("LocalizeStringEvent", StringComparison.Ordinal)
            || typeName.Contains("GameObjectLocalizer", StringComparison.Ordinal)
            || typeName.Contains("LocalizedText", StringComparison.Ordinal)
            || typeName.Contains("LocalizationBehaviour", StringComparison.Ordinal)
            || typeName.Contains("ODDLocali", StringComparison.Ordinal)
            || (typeName.Contains("Localiz", StringComparison.Ordinal) && typeName.Contains("UnityEngine.Localization", StringComparison.Ordinal));
    }

    /// <summary>
    /// Strip non-UnityEngine/TMPro components from the cloned button (the native template
    /// has game-specific behaviours wired to scene listeners we do not want to invoke).
    /// We keep <see cref="Button_ODD"/> so the native visuals/hover effects survive,
    /// but disconnect its click listeners; clicks are handled by the click interceptor.
    /// </summary>
    private static void StripNonUnityNonNativeComponents(GameObject root)
    {
        Component[] components;
        try
        {
            components = root.GetComponentsInChildren<Component>(true);
        }
        catch
        {
            return;
        }

        for (var i = 0; i < components.Length; i++)
        {
            var component = components[i];
            if (component is null || component is Transform)
            {
                continue;
            }

            // Keep the game's Button_ODD so native click visuals stay intact, but kill its
            // connected event listeners so clicking doesn't trigger old run-time logic.
            if (component is Button_ODD oddButton)
            {
                try
                {
                    oddButton.onClick.RemoveAllListeners();
                }
                catch
                {
                }

                continue;
            }

            // Keep core Unity/TMPro UI components (transforms, images, layout, etc.).
            var ns = component.GetType().Namespace ?? string.Empty;
            if (ns.StartsWith("UnityEngine", StringComparison.Ordinal) ||
                ns.StartsWith("TMPro", StringComparison.Ordinal))
            {
                if (component is Button uiButton)
                {
                    try
                    {
                        uiButton.onClick.RemoveAllListeners();
                    }
                    catch
                    {
                    }
                }

                continue;
            }

            // Drop everything else (game-specific behaviours, audio cues tied to scene).
            try
            {
                UnityEngine.Object.Destroy(component);
            }
            catch
            {
            }
        }
    }

    private static bool IsCrabtyaObject(Transform transform)
    {
        var current = transform;
        while (current is not null)
        {
            var name = current.name ?? string.Empty;
            if (name.StartsWith("Crabtya.", StringComparison.Ordinal) || name.StartsWith("EIC.ModLoader", StringComparison.Ordinal))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static string BuildPath(Transform transform)
    {
        var nodes = new List<string>();
        var current = transform;
        while (current is not null && nodes.Count < 16)
        {
            nodes.Add(current.name ?? string.Empty);
            current = current.parent;
        }

        nodes.Reverse();
        return string.Join("/", nodes);
    }

    private static void DestroyNativeButton()
    {
        RestoreShiftedRect();

        if (_buttonObject != null)
        {
            UnityEngine.Object.Destroy(_buttonObject);
        }

        _buttonObject = null;
        _buttonParent = null;
        _pauseScreen = null;
    }

    private static void RestoreShiftedRect()
    {
        if (_shiftedRect is not null)
        {
            try
            {
                if ((UnityEngine.Object)_shiftedRect != null)
                {
                    _shiftedRect.anchoredPosition = _shiftedOriginalPosition;
                }
            }
            catch
            {
            }
        }

        _shiftedRect = null;
    }
}
