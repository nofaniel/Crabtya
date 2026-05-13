using TMPro;
using UnityEngine;
using UnityEngine.UI;
using World.UI.MainMenuScreen;

namespace EIC.ModLoader;

public static class MainMenuModsButtonApplicator
{
    private const string CrabtyaButtonName = "Crabtya.MainMenu.ModsButton";
    private const string ModsLabel = "MODS";

    private static GameObject? _buttonObject;
    private static Transform? _buttonParent;
    private static MainMenuScreenUI? _mainMenuScreen;
    private static float _nextProbe;
    private static bool _loggedCreation;
    private static bool _loggedFallbackCreation;
    private static bool _loggedMissingMainMenuButtons;
    private static bool _loggedMainMenuProbe;
    private static string _lastMainMenuSignature = string.Empty;
    private static bool _buttonPreviouslyPresent;
    private static readonly List<ShiftedRectState> ShiftedRects = new();

    public static bool IsMainMenuLikelyVisible()
    {
        return TryGetMainMenuContext(out _, out _, out _) || IsMainMenuContextLikelyActive();
    }

    public static void Update()
    {
        RefreshButtonStateForRetryLogging();

        if (Time.unscaledTime < _nextProbe)
        {
            return;
        }

        _nextProbe = Time.unscaledTime + 0.2f;
        if (TryGetMainMenuContext(out var screen, out var settingsButton, out var signature))
        {
            _loggedMissingMainMenuButtons = false;
            if (!_loggedMainMenuProbe || !string.Equals(_lastMainMenuSignature, signature, StringComparison.Ordinal))
            {
                _loggedMainMenuProbe = true;
                _lastMainMenuSignature = signature;
                Plugin.Instance?.Log.LogInfo("Crabtya main-menu probe: found native menu context.");
                Plugin.Instance?.Log.LogInfo($"Crabtya main-menu probe details: {signature}");
            }

            var templateRect = settingsButton.GetComponent<RectTransform>();
            var parent = settingsButton.transform.parent;
            if (templateRect == null || parent == null)
            {
                return;
            }

            var existingNativeButton = FindNativeButton(parent);
            if (existingNativeButton is not null)
            {
                _buttonObject = existingNativeButton;
                _buttonParent = parent;
                _mainMenuScreen = screen;
                _buttonPreviouslyPresent = true;
                StripLocalizationComponents(existingNativeButton);
                TrySetButtonLabel(existingNativeButton, ModsLabel);
                EnsureClickHandler(existingNativeButton);
                return;
            }

            try
            {
                Plugin.Instance?.Log.LogInfo("Crabtya main-menu native Mods button missing; creating now.");
                if (!CreateButtonFromTemplate(screen, settingsButton, parent))
                {
                    Plugin.Instance?.Log.LogWarning("Crabtya main-menu Mods button creation attempt did not produce a native clone.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance?.Log.LogWarning($"Crabtya main-menu Mods button creation failed: {ex}");
            }

            return;
        }

        if (!_loggedMissingMainMenuButtons)
        {
            _loggedMissingMainMenuButtons = true;
            Plugin.Instance?.Log.LogInfo("Crabtya main-menu probe: no MainMenuScreenUI/settings button pair was found.");
        }

        _loggedMainMenuProbe = false;
        _lastMainMenuSignature = string.Empty;

        DestroyNativeButton();
    }

    private static void RefreshButtonStateForRetryLogging()
    {
        var hasTrackedButton = _buttonObject != null;
        if (hasTrackedButton)
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
        _mainMenuScreen = null;
        _loggedCreation = false;
        if (_loggedFallbackCreation)
        {
            _loggedFallbackCreation = false;
        }

        _loggedMissingMainMenuButtons = false;
        _loggedMainMenuProbe = false;
        _lastMainMenuSignature = string.Empty;
    }

    private static bool TryGetMainMenuContext(out MainMenuScreenUI screen, out Button_ODD settingsButton, out string signature)
    {
        screen = null!;
        settingsButton = null!;
        signature = string.Empty;

        MainMenuScreenUI[] screens;
        try
        {
            screens = Resources.FindObjectsOfTypeAll<MainMenuScreenUI>();
        }
        catch
        {
            return false;
        }

        for (var i = 0; i < screens.Length; i++)
        {
            var candidate = screens[i];
            if (candidate is null ||
                candidate.gameObject is null ||
                !candidate.gameObject.activeInHierarchy ||
                IsCrabtyaObject(candidate.transform))
            {
                continue;
            }

            var candidateSettingsButton = candidate._settingsButton;
            if (candidateSettingsButton is null ||
                candidateSettingsButton.gameObject is null)
            {
                continue;
            }

            screen = candidate;
            settingsButton = candidateSettingsButton;
            signature = BuildProbeSignature(candidate);
            return true;
        }

        return false;
    }

    private static bool CreateButtonFromTemplate(MainMenuScreenUI screen, Button_ODD settingsButton, Transform parent)
    {
        DestroyNativeButton();

        Plugin.Instance?.Log.LogInfo(
            $"Crabtya main-menu creating Mods button from native template. Parent='{BuildPath(parent)}', SettingsPos={settingsButton.GetComponent<RectTransform>()?.anchoredPosition}, QuitPos={screen._quitButton?.GetComponent<RectTransform>()?.anchoredPosition}");

        var clone = UnityEngine.Object.Instantiate(settingsButton.gameObject, parent);
        clone.name = CrabtyaButtonName;
        clone.SetActive(true);

        var cloneRect = clone.GetComponent<RectTransform>();
        var templateRect = settingsButton.GetComponent<RectTransform>();
        if (cloneRect is null || templateRect is null)
        {
            UnityEngine.Object.Destroy(clone);
            return false;
        }

        cloneRect.anchorMin = templateRect.anchorMin;
        cloneRect.anchorMax = templateRect.anchorMax;
        cloneRect.pivot = templateRect.pivot;
        cloneRect.sizeDelta = templateRect.sizeDelta;
        cloneRect.localScale = UiScaleApplicator.GetCapturedOrCurrentLocalScale(templateRect);
        cloneRect.localRotation = templateRect.localRotation;

        var siblingIndex = DetermineInsertionSiblingIndex(screen, settingsButton, parent);
        clone.transform.SetSiblingIndex(siblingIndex);

        var hasLayoutGroup = parent.GetComponent<LayoutGroup>() is not null;
        var targetPosition = templateRect.anchoredPosition;
        if (!hasLayoutGroup)
        {
            targetPosition = PositionInsertedButton(screen, settingsButton, cloneRect);
            cloneRect.anchoredPosition = targetPosition;
        }

        var strippedLocalizers = StripLocalizationComponents(clone);
        if (strippedLocalizers > 0)
        {
            Plugin.Instance?.Log.LogInfo($"Crabtya main-menu Mods button: stripped {strippedLocalizers} localization component(s) on clone.");
        }

        if (!TrySetButtonLabel(clone, ModsLabel))
        {
            Plugin.Instance?.Log.LogWarning($"Crabtya main-menu Mods button created but no supported text component was found on '{BuildPath(clone.transform)}'.");
        }

        InstallClickInterceptor(clone);

        _buttonObject = clone;
        _buttonParent = parent;
        _mainMenuScreen = screen;
        _buttonPreviouslyPresent = true;

        if (!_loggedCreation)
        {
            _loggedCreation = true;
            Plugin.Instance?.Log.LogInfo(
                $"Crabtya main-menu Mods button created. Parent='{BuildPath(parent)}', Template='{GetButtonLabel(settingsButton)}', SiblingIndex={siblingIndex}, HasLayoutGroup={hasLayoutGroup}, AnchoredPosition={targetPosition}");
        }

        return true;
    }

    private static int DetermineInsertionSiblingIndex(MainMenuScreenUI screen, Button_ODD settingsButton, Transform parent)
    {
        var quitButton = screen._quitButton;
        if (quitButton is not null &&
            quitButton.gameObject is not null &&
            quitButton.transform.parent == parent)
        {
            return quitButton.transform.GetSiblingIndex();
        }

        return Math.Min(parent.childCount - 1, settingsButton.transform.GetSiblingIndex() + 1);
    }

    private static Vector2 PositionInsertedButton(MainMenuScreenUI screen, Button_ODD settingsButton, RectTransform cloneRect)
    {
        var settingsRect = settingsButton.GetComponent<RectTransform>();
        if (settingsRect is null)
        {
            return cloneRect.anchoredPosition;
        }

        var parent = settingsRect.parent;
        if (parent is null)
        {
            return cloneRect.anchoredPosition;
        }

        RestoreShiftedRects();

        var nativeRects = new List<RectTransform>();
        for (var i = 0; i < parent.childCount; i++)
        {
            if (parent.GetChild(i) is RectTransform rect &&
                rect != cloneRect &&
                !IsCrabtyaObject(rect) &&
                IsButtonLikeRect(rect))
            {
                nativeRects.Add(rect);
            }
        }

        if (nativeRects.Count < 2)
        {
            var fallbackDelta = ResolveInsertionDeltaY(screen, settingsRect);
            ShiftSiblingsForInsertion(cloneRect.transform, fallbackDelta);
            return settingsRect.anchoredPosition + new Vector2(0f, fallbackDelta);
        }

        var settingsY = settingsRect.anchoredPosition.y;
        var quitRect = screen._quitButton?.GetComponent<RectTransform>();
        var shouldPreserveBottomSlot = quitRect is not null && quitRect.parent == parent;
        float insertionDeltaY;
        if (shouldPreserveBottomSlot)
        {
            insertionDeltaY = quitRect!.anchoredPosition.y - settingsY;
        }
        else
        {
            var spacing = EstimateNativeSpacing(nativeRects.OrderByDescending(r => r.anchoredPosition.y).ToList());
            if (spacing < 1f)
            {
                spacing = 40f;
            }

            var flowDirection = EstimateVerticalFlowDirection(nativeRects);
            insertionDeltaY = flowDirection < 0 ? -spacing : spacing;
        }

        if (Math.Abs(insertionDeltaY) < 1f)
        {
            insertionDeltaY = -40f;
        }

        var modsX = settingsRect.anchoredPosition.x;
        var upperStackDeltaY = shouldPreserveBottomSlot ? -insertionDeltaY : 0f;
        var modsY = settingsY + insertionDeltaY + upperStackDeltaY;

        Plugin.Instance?.Log.LogInfo($"Crabtya main-menu insertion: nativeButtons={nativeRects.Count}, insertionDeltaY={insertionDeltaY:0.0}, upperStackDeltaY={upperStackDeltaY:0.0}, settingsY={settingsY:0.0}, modsY={modsY:0.0}, preserveBottomSlot={shouldPreserveBottomSlot}.");

        if (shouldPreserveBottomSlot)
        {
            ShiftSiblingsPreservingBottomSlot(cloneRect.transform, settingsRect.transform, insertionDeltaY);
        }
        else
        {
            ShiftSiblingsForInsertion(cloneRect.transform, insertionDeltaY);
        }

        return new Vector2(modsX, modsY);
    }

    private static bool IsButtonLikeRect(RectTransform rect)
    {
        try
        {
            if (rect.GetComponent<Button>() is not null)
            {
                return true;
            }

            if (rect.GetComponent<Selectable>() is not null)
            {
                return true;
            }

            if (rect.GetComponent<Button_ODD>() is not null)
            {
                return true;
            }

            var components = rect.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component is null)
                {
                    continue;
                }

                var typeName = component.GetType().FullName ?? string.Empty;
                if (typeName.Contains("Button", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    private static float EstimateNativeSpacing(IReadOnlyList<RectTransform> ordered)
    {
        if (ordered.Count < 2)
        {
            return 0f;
        }

        var gaps = new List<float>();
        for (var i = 1; i < ordered.Count; i++)
        {
            var gap = Math.Abs(ordered[i - 1].anchoredPosition.y - ordered[i].anchoredPosition.y);
            if (gap > 4f && gap < 260f)
            {
                gaps.Add(gap);
            }
        }

        if (gaps.Count == 0)
        {
            return 0f;
        }

        gaps.Sort();
        return gaps[gaps.Count / 2];
    }

    private static float ResolveInsertionDeltaY(MainMenuScreenUI screen, RectTransform settingsRect)
    {
        var quitRect = screen._quitButton?.GetComponent<RectTransform>();
        if (quitRect is not null && quitRect.parent == settingsRect.parent)
        {
            var directDelta = quitRect.anchoredPosition.y - settingsRect.anchoredPosition.y;
            if (Math.Abs(directDelta) > 8f)
            {
                return directDelta;
            }
        }

        var siblings = new List<RectTransform>();
        for (var i = 0; i < settingsRect.parent.childCount; i++)
        {
            if (settingsRect.parent.GetChild(i) is RectTransform rect && !IsCrabtyaObject(rect))
            {
                siblings.Add(rect);
            }
        }

        var yValues = siblings
            .Select(r => r.anchoredPosition.y)
            .ToList();
        if (yValues.Count == 0)
        {
            return -130f;
        }

        var spacing = EstimateSpacing(yValues, 130f);
        var flowDirection = EstimateVerticalFlowDirection(siblings);
        return flowDirection < 0 ? -spacing : spacing;
    }

    private static void ShiftSiblingsForInsertion(Transform insertedButton, float deltaY)
    {
        RestoreShiftedRects();

        var parent = insertedButton.parent;
        if (parent == null)
        {
            return;
        }

        var insertedIndex = insertedButton.GetSiblingIndex();
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child == insertedButton ||
                IsCrabtyaObject(child) ||
                child.GetSiblingIndex() <= insertedIndex)
            {
                continue;
            }

            var rect = child.GetComponent<RectTransform>();
            if (rect is null)
            {
                continue;
            }

            ShiftedRects.Add(new ShiftedRectState(rect, rect.anchoredPosition));
            rect.anchoredPosition += new Vector2(0f, deltaY);
        }
    }

    private static void ShiftSiblingsPreservingBottomSlot(Transform insertedButton, Transform insertAfter, float downstreamDeltaY)
    {
        RestoreShiftedRects();

        var parent = insertedButton.parent;
        if (parent == null)
        {
            return;
        }

        var insertAfterIndex = insertAfter.GetSiblingIndex();
        var insertedIndex = insertedButton.GetSiblingIndex();
        var upperStackDeltaY = -downstreamDeltaY;
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child == insertedButton || IsCrabtyaObject(child))
            {
                continue;
            }

            var rect = child.GetComponent<RectTransform>();
            if (rect is null)
            {
                continue;
            }

            var childIndex = child.GetSiblingIndex();
            var deltaY = childIndex <= insertAfterIndex ? upperStackDeltaY : 0f;
            if (Math.Abs(deltaY) < 0.01f)
            {
                continue;
            }

            ShiftedRects.Add(new ShiftedRectState(rect, rect.anchoredPosition));
            rect.anchoredPosition += new Vector2(0f, deltaY);
        }
    }

    private static void RestoreShiftedRects()
    {
        for (var i = 0; i < ShiftedRects.Count; i++)
        {
            var state = ShiftedRects[i];
            if (state.Rect is not null)
            {
                state.Rect.anchoredPosition = state.OriginalAnchoredPosition;
            }
        }

        ShiftedRects.Clear();
    }

    private static int EstimateVerticalFlowDirection(IReadOnlyList<RectTransform> buttons)
    {
        var ordered = buttons
            .OrderBy(v => v.transform.GetSiblingIndex())
            .ToList();
        var deltas = new List<float>();
        for (var i = 1; i < ordered.Count; i++)
        {
            deltas.Add(ordered[i].anchoredPosition.y - ordered[i - 1].anchoredPosition.y);
        }

        if (deltas.Count == 0)
        {
            return -1;
        }

        var descendingVotes = deltas.Count(d => d < 0f);
        var ascendingVotes = deltas.Count(d => d > 0f);
        if (descendingVotes == ascendingVotes)
        {
            return -1;
        }

        return descendingVotes > ascendingVotes ? -1 : 1;
    }

    private static float EstimateSpacing(IReadOnlyList<float> yValues, float fallback)
    {
        var ordered = yValues
            .Distinct()
            .OrderByDescending(v => v)
            .ToList();
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

    private static string GetButtonLabel(Button button)
    {
        var uiText = button.GetComponentInChildren<Text>(true);
        var label = NormalizeLabel(uiText?.text);
        if (!string.IsNullOrWhiteSpace(label))
        {
            return label;
        }

        var tmpText = button.GetComponentInChildren<TMP_Text>(true);
        label = NormalizeLabel(tmpText?.text);
        return string.IsNullOrWhiteSpace(label) ? string.Empty : label;
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

    private static void EnsureClickHandler(GameObject buttonObject)
    {
        InstallClickInterceptor(buttonObject);
    }

    private static void InstallClickInterceptor(GameObject buttonObject)
    {
        const string OverlayName = "Crabtya.ClickInterceptor";

        DisableNativeButtonRaycasts(buttonObject);

        Transform? existing = null;
        for (var i = 0; i < buttonObject.transform.childCount; i++)
        {
            var child = buttonObject.transform.GetChild(i);
            if (string.Equals(child.name, OverlayName, StringComparison.Ordinal))
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
            overlay = new GameObject(OverlayName);
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
            if (owner is null || string.Equals(owner.name, "Crabtya.ClickInterceptor", StringComparison.Ordinal))
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

    private static string NormalizeLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var text = new System.Text.StringBuilder(trimmed.Length);
        var insideTag = false;
        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (c == '<')
            {
                insideTag = true;
                continue;
            }

            if (c == '>' && insideTag)
            {
                insideTag = false;
                continue;
            }

            if (!insideTag)
            {
                text.Append(c);
            }
        }

        return text
            .ToString()
            .Replace('\n', ' ')
            .Replace('\r', ' ')
            .Trim();
    }

    private static string BuildProbeSignature(MainMenuScreenUI screen)
    {
        var settings = screen._settingsButton is null
            ? "<missing>"
            : $"{GetButtonLabel(screen._settingsButton)}@{screen._settingsButton.transform.GetSiblingIndex()}";
        var quit = screen._quitButton is null
            ? "<missing>"
            : $"{GetButtonLabel(screen._quitButton)}@{screen._quitButton.transform.GetSiblingIndex()}";
        return $"{BuildPath(screen.transform)} | Settings={settings} | Quit={quit}";
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

    private static void DestroyUi()
    {
        RestoreShiftedRects();

        if (_buttonObject != null)
        {
            UnityEngine.Object.Destroy(_buttonObject);
        }

        _buttonObject = null;
        _buttonParent = null;
        _mainMenuScreen = null;
        _buttonPreviouslyPresent = false;
    }

    private static void DestroyNativeButton()
    {
        RestoreShiftedRects();

        if (_buttonObject != null)
        {
            UnityEngine.Object.Destroy(_buttonObject);
        }

        _buttonObject = null;
        _buttonParent = null;
        _mainMenuScreen = null;
    }

    private static GameObject? FindNativeButton(Transform parent)
    {
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (!string.Equals(child.name, CrabtyaButtonName, StringComparison.Ordinal))
            {
                continue;
            }

            return child.gameObject;
        }

        return null;
    }

    private static bool IsMainMenuContextLikelyActive()
    {
        if (IsGameplayLikelyActive())
        {
            return false;
        }

        Component[] components;
        try
        {
            components = UnityEngine.Object.FindObjectsOfType<Component>();
        }
        catch
        {
            return false;
        }

        for (var i = 0; i < components.Length; i++)
        {
            var component = components[i];
            if (component is null || !component.gameObject.activeInHierarchy)
            {
                continue;
            }

            var typeName = component.GetType().FullName ?? string.Empty;
            if (typeName.Contains("MainMenu", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGameplayLikelyActive()
    {
        Component[] components;
        try
        {
            components = UnityEngine.Object.FindObjectsOfType<Component>();
        }
        catch
        {
            return false;
        }

        for (var i = 0; i < components.Length; i++)
        {
            var component = components[i];
            if (component is null || !component.gameObject.activeInHierarchy)
            {
                continue;
            }

            var typeName = component.GetType().FullName ?? string.Empty;
            if (string.Equals(typeName, "World.Characters.PlayerCharacter", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct ShiftedRectState(RectTransform Rect, Vector2 OriginalAnchoredPosition);
}
