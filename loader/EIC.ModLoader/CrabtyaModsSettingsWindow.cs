using System.Globalization;
using System.Linq;
using System.Text.Json;
using Crabtya.ModApi;
using Il2CppInterop.Runtime.Injection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using World.UI;
using World.UI.MainMenuScreen;

namespace EIC.ModLoader;

public static class CrabtyaModsSettingsWindow
{
    private const string RootName = "Crabtya.ModsSettingsWindow";
    private const string ClickInterceptorName = "Crabtya.ClickInterceptor";
    private const float HeaderRowHeight = 78f;
    private const float SettingRowHeight = 52f;
    private const float ModBlockPadding = 16f;
    private const float PanelInset = 20f;

    private static readonly Color FallbackPanelColor = new(0.82f, 0.72f, 0.52f, 1f);
    private static readonly Color FallbackPanelBorderColor = new(0.22f, 0.14f, 0.08f, 1f);
    private static readonly Color FallbackRowBgColor = new(0.86f, 0.78f, 0.58f, 1f);
    private static readonly Color FallbackRowAltBgColor = new(0.72f, 0.62f, 0.42f, 1f);
    private static readonly Color FallbackHeaderTextColor = new(0.98f, 0.92f, 0.76f, 1f);
    private static readonly Color FallbackTextColor = new(0.12f, 0.06f, 0.02f, 1f);
    private static readonly Color FallbackTextMutedColor = new(0.36f, 0.22f, 0.10f, 0.95f);
    private static readonly Color FallbackCloseBgColor = new(0.52f, 0.38f, 0.24f, 1f);
    private static readonly Color FallbackEnabledColor = new(0.28f, 0.48f, 0.22f, 1f);
    private static readonly Color FallbackDisabledColor = new(0.58f, 0.28f, 0.24f, 1f);
    private static readonly Color FallbackOptionBgColor = new(0.72f, 0.58f, 0.38f, 1f);
    private static readonly Color FallbackSliderBgColor = new(0.38f, 0.24f, 0.14f, 1f);
    private static readonly Color FallbackSliderFillColor = new(0.46f, 0.30f, 0.18f, 1f);
    private static readonly Color FallbackSliderHandleColor = new(0.92f, 0.82f, 0.62f, 1f);
    private static readonly Color FallbackBackdropColor = new(0f, 0f, 0f, 0.74f);

    private static bool _registered;
    private static GameObject? _root;
    private static RectTransform? _scrollContent;
    private static ScrollRect? _scrollRect;
    private static TextMeshProUGUI? _hintText;
    private static bool _visible;
    private static TMP_FontAsset? _nativeFont;
    private static GameObject? _nativeButtonTemplate;
    private static Slider? _nativeSliderTemplate;
    private static Sprite? _nativeButtonSprite;
    private static Sprite? _pixelPanelSprite;
    private static bool _themeResolved;
    private static bool _themeLogWritten;

    private static Color _panelColor = FallbackPanelColor;
    private static Color _panelBorderColor = FallbackPanelBorderColor;
    private static Color _rowBgColor = FallbackRowBgColor;
    private static Color _rowAltBgColor = FallbackRowAltBgColor;
    private static Color _headerTextColor = FallbackHeaderTextColor;
    private static Color _textColor = FallbackTextColor;
    private static Color _textMutedColor = FallbackTextMutedColor;
    private static Color _closeBgColor = FallbackCloseBgColor;
    private static Color _enabledColor = FallbackEnabledColor;
    private static Color _disabledColor = FallbackDisabledColor;
    private static Color _optionBgColor = FallbackOptionBgColor;
    private static Color _sliderBgColor = FallbackSliderBgColor;
    private static Color _sliderFillColor = FallbackSliderFillColor;
    private static Color _sliderHandleColor = FallbackSliderHandleColor;
    private static Color _backdropColor = FallbackBackdropColor;

    public static bool IsVisible => _visible && _root != null;

    public static void EnsureCreated()
    {
        if (!_registered)
        {
            ClassInjector.RegisterTypeInIl2Cpp<CrabtyaModsSettingsWindowBehaviour>();
            _registered = true;
        }

        if (_root != null)
        {
            return;
        }

        ResolveThemeAndTemplates();

        _root = new GameObject(RootName);
        UnityEngine.Object.DontDestroyOnLoad(_root);
        _root.SetActive(false);

        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 1;

        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _root.AddComponent<GraphicRaycaster>();
        _root.AddComponent<CrabtyaModsSettingsWindowBehaviour>();

        BuildChrome();

        Plugin.Instance?.Log.LogInfo("Crabtya mods settings window created.");
    }

    public static void Show()
    {
        try
        {
            EnsureCreated();
            if (_root is null)
            {
                return;
            }

            _root.SetActive(true);
            _visible = true;
            Refresh();
            Plugin.Instance?.Log.LogInfo("Crabtya mods settings window opened.");
        }
        catch (Exception ex)
        {
            Plugin.Instance?.Log.LogWarning($"Crabtya mods settings window show failed: {ex}");
        }
    }

    public static void Hide()
    {
        if (_root is null)
        {
            return;
        }

        _root.SetActive(false);
        _visible = false;
    }

    public static void Toggle()
    {
        if (_visible)
        {
            Hide();
            return;
        }

        Show();
    }

    private static void BuildChrome()
    {
        if (_root is null)
        {
            return;
        }

        var backdrop = new GameObject("Backdrop");
        backdrop.transform.SetParent(_root.transform, false);
        var backdropRect = backdrop.AddComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        var backdropImage = backdrop.AddComponent<Image>();
        backdropImage.color = _backdropColor;
        backdropImage.raycastTarget = true;

        // Outer dark frame ring (drawn below the panel for a chunky pixel-art border).
        var outerFrame = new GameObject("OuterFrame");
        outerFrame.transform.SetParent(_root.transform, false);
        var outerRect = outerFrame.AddComponent<RectTransform>();
        outerRect.anchorMin = new Vector2(0.5f, 0.5f);
        outerRect.anchorMax = new Vector2(0.5f, 0.5f);
        outerRect.pivot = new Vector2(0.5f, 0.5f);
        outerRect.anchoredPosition = Vector2.zero;
        outerRect.sizeDelta = new Vector2(964f, 704f);
        var outerImage = outerFrame.AddComponent<Image>();
        outerImage.color = ScaleColor(_panelBorderColor, 0.6f, 1f);
        outerImage.raycastTarget = false;

        var panel = new GameObject("Panel");
        panel.transform.SetParent(_root.transform, false);
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(940f, 680f);
        var panelImage = panel.AddComponent<Image>();
        _pixelPanelSprite ??= CreateRoundedPixelSprite(size: 48, cornerRadius: 0, borderInset: 0);
        panelImage.sprite = _pixelPanelSprite;
        panelImage.type = Image.Type.Sliced;
        panelImage.color = _panelColor;
        // Always draw a strong outer border around the panel for pixel-art definition,
        // regardless of whether a 9-slice sprite is present.
        AddBorder(panel.transform, 6f, _panelBorderColor);
        // Inner highlight along the top edge for a subtle bevel that reads as paper/wood grain.
        AddBorderEdge(
            panel.transform,
            "Border.Inner.TopHighlight",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(-12f, 3f),
            new Vector2(0f, -10f),
            ScaleColor(_panelColor, 1.18f, 0.85f));

        var innerFrame = new GameObject("InnerFrame");
        innerFrame.transform.SetParent(panel.transform, false);
        var innerRect = innerFrame.AddComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(PanelInset, PanelInset + 32f);
        innerRect.offsetMax = new Vector2(-PanelInset, -PanelInset - 42f);
        // Removed inner frame background and border to match native menu feel.

        var title = new GameObject("Title");
        title.transform.SetParent(panel.transform, false);
        var titleRect = title.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -28f);
        titleRect.sizeDelta = new Vector2(0f, 48f);
        var titleText = AddTmp(title, "MODS", 42, TextAlignmentOptions.Center, FontStyles.Bold);
        titleText.color = _textColor;
        titleText.raycastTarget = false;
        titleText.characterSpacing = 8f;

        var hint = new GameObject("Hint");
        hint.transform.SetParent(panel.transform, false);
        var hintRect = hint.AddComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.27f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 16f);
        hintRect.sizeDelta = new Vector2(0f, 28f);
        _hintText = AddTmp(hint, "Camera/visual mods apply live.  DLL & catalog mods require restart.  Press CLOSE to dismiss.", 15, TextAlignmentOptions.Center, FontStyles.Italic);
        _hintText.color = _textMutedColor;
        _hintText.raycastTarget = false;

        var closeBinding = CreateButtonBinding(
            panel.transform,
            "Close",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-26f, -26f),
            new Vector2(180f, 48f),
            "CLOSE",
            _closeBgColor,
            _headerTextColor,
            20f,
            FontStyles.Bold,
            TextAlignmentOptions.Center);
        closeBinding.Button.onClick.AddListener((Action)Hide);

        var openFolderBinding = CreateButtonBinding(
            panel.transform,
            "OpenModsFolder",
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(26f, 16f),
            new Vector2(220f, 48f),
            "OPEN MODS FOLDER",
            _closeBgColor,
            _headerTextColor,
            15f,
            FontStyles.Bold,
            TextAlignmentOptions.Center);
        openFolderBinding.Button.onClick.AddListener((Action)OpenModsFolder);

        // Scroll area container (same inset as the old flat Content rect).
        var scrollArea = new GameObject("ScrollArea");
        scrollArea.transform.SetParent(panel.transform, false);
        var scrollAreaRect = scrollArea.AddComponent<RectTransform>();
        scrollAreaRect.anchorMin = new Vector2(0f, 0f);
        scrollAreaRect.anchorMax = new Vector2(1f, 1f);
        scrollAreaRect.pivot = new Vector2(0.5f, 0.5f);
        scrollAreaRect.offsetMin = new Vector2(30f, 72f);
        scrollAreaRect.offsetMax = new Vector2(-24f, -96f);

        // Vertical scrollbar — sits on the right edge of the scroll area.
        var scrollbarGo = new GameObject("Scrollbar");
        scrollbarGo.transform.SetParent(scrollArea.transform, false);
        var scrollbarGoRect = scrollbarGo.AddComponent<RectTransform>();
        scrollbarGoRect.anchorMin = new Vector2(1f, 0f);
        scrollbarGoRect.anchorMax = new Vector2(1f, 1f);
        scrollbarGoRect.pivot = new Vector2(1f, 0.5f);
        scrollbarGoRect.anchoredPosition = Vector2.zero;
        scrollbarGoRect.sizeDelta = new Vector2(18f, 0f);
        var scrollbarBgImg = scrollbarGo.AddComponent<Image>();
        scrollbarBgImg.color = new Color(0f, 0f, 0f, 0.1f);
        scrollbarBgImg.raycastTarget = true;

        var slidingArea = new GameObject("SlidingArea");
        slidingArea.transform.SetParent(scrollbarGo.transform, false);
        var slidingAreaRect = slidingArea.AddComponent<RectTransform>();
        slidingAreaRect.anchorMin = Vector2.zero;
        slidingAreaRect.anchorMax = Vector2.one;
        slidingAreaRect.offsetMin = new Vector2(2f, 2f);
        slidingAreaRect.offsetMax = new Vector2(-2f, -2f);

        var handleGo = new GameObject("Handle");
        handleGo.transform.SetParent(slidingArea.transform, false);
        var handleGoRect = handleGo.AddComponent<RectTransform>();
        handleGoRect.anchorMin = new Vector2(0f, 0f);
        handleGoRect.anchorMax = new Vector2(1f, 0.2f);
        handleGoRect.offsetMin = Vector2.zero;
        handleGoRect.offsetMax = Vector2.zero;
        var handleImg = handleGo.AddComponent<Image>();
        handleImg.color = _sliderHandleColor;
        handleImg.raycastTarget = true;

        var scrollbar = scrollbarGo.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleGoRect;
        scrollbar.targetGraphic = handleImg;

        // Viewport — clips the scrollable content, leaves room for the scrollbar.
        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollArea.transform, false);
        var viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = new Vector2(-22f, 0f);
        var viewportImg = viewport.AddComponent<Image>();
        viewportImg.color = Color.clear;
        viewportImg.raycastTarget = true;
        viewport.AddComponent<RectMask2D>();

        // ContentList — the actual scrollable container; height is set dynamically in Refresh().
        var contentList = new GameObject("ContentList");
        contentList.transform.SetParent(viewport.transform, false);
        var contentListRect = contentList.AddComponent<RectTransform>();
        contentListRect.anchorMin = new Vector2(0f, 1f);
        contentListRect.anchorMax = new Vector2(1f, 1f);
        contentListRect.pivot = new Vector2(0f, 1f);
        contentListRect.anchoredPosition = Vector2.zero;
        contentListRect.sizeDelta = new Vector2(0f, 100f);
        _scrollContent = contentListRect;

        // ScrollRect — wires viewport, content list, and scrollbar together.
        var scrollRectComp = scrollArea.AddComponent<ScrollRect>();
        scrollRectComp.viewport = viewportRect;
        scrollRectComp.content = contentListRect;
        scrollRectComp.vertical = true;
        scrollRectComp.horizontal = false;
        scrollRectComp.verticalScrollbar = scrollbar;
        scrollRectComp.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scrollRectComp.movementType = ScrollRect.MovementType.Clamped;
        scrollRectComp.scrollSensitivity = 25f;
        scrollRectComp.inertia = true;
        scrollRectComp.decelerationRate = 0.15f;
        _scrollRect = scrollRectComp;
    }

    public static void Refresh(float? scrollPosition = null)
    {
        if (_scrollContent is null)
        {
            return;
        }

        ResolveThemeAndTemplates();
        ResetContentRect();

        for (var i = _scrollContent.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.Destroy(_scrollContent.GetChild(i).gameObject);
        }

        var gameRoot = AppContext.BaseDirectory;
        var stateStorePath = Path.Combine(gameRoot, "Mods", "mod-state.json");
        var startupQueuePath = Path.Combine(gameRoot, "Mods", "startup-commands.json");

        ModStateFile state;
        try
        {
            state = new ModStateStore(stateStorePath).Load();
        }
        catch
        {
            state = new ModStateFile();
        }

        var pendingToggles = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var toggle in StartupCommandQueue.Peek(startupQueuePath))
            {
                if (!string.IsNullOrWhiteSpace(toggle.ModId))
                {
                    pendingToggles[toggle.ModId] = toggle.Enabled;
                }
            }
        }
        catch
        {
        }

        var settingsByMod = CrabtyaSettingsRegistry.GetAllSettings()
            .GroupBy(s => s.ModId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var cameraPatchByMod = RuntimeContentState.Current.CameraPatches.Values
            .Where(p => p.Settings.Slider)
            .Where(p => string.Equals(p.Target, "camera.orthographicSize", StringComparison.OrdinalIgnoreCase))
            .GroupBy(p => p.ModId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

        var mouseWheelInvertByMod = RuntimeContentState.Current.CameraPatches.Values
            .Where(p => p.Settings.MouseWheel)
            .GroupBy(p => p.ModId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

        var uiScaleByMod = RuntimeContentState.Current.UiScales.Values
            .Where(p => p.Settings.Slider)
            .GroupBy(p => p.ModId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

        var mods = state.Mods
            .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var y = 0f;
        var modRowIndex = 0;
        if (mods.Count == 0)
        {
            CreateInfoLabel(_scrollContent, "No mods installed. Drop a mod folder into game/Mods/ and relaunch.", ref y);
        }
        else
        {
            foreach (var pair in mods)
            {
                var modId = pair.Key;
                var entry = pair.Value;

                // Create a container for the entire mod block to give it a unified "menu row" look.
                var blockObj = new GameObject($"ModBlock.{modId}");
                blockObj.transform.SetParent(_scrollContent, false);
                var blockRect = blockObj.AddComponent<RectTransform>();
                blockRect.anchorMin = new Vector2(0f, 1f);
                blockRect.anchorMax = new Vector2(1f, 1f);
                blockRect.pivot = new Vector2(0f, 1f);
                blockRect.anchoredPosition = new Vector2(0f, y);
                
                var blockImage = blockObj.AddComponent<Image>();
                blockImage.sprite = _pixelPanelSprite;
                blockImage.type = Image.Type.Sliced;
                blockImage.color = _rowBgColor;
                AddBorder(blockObj.transform, 4f, _panelBorderColor);
                blockImage.raycastTarget = false;

                var blockY = -8f; // Start with some top padding

                CreateModHeader(blockRect, modId, entry, pendingToggles, startupQueuePath, modRowIndex++, ref blockY);

                if (entry.ConflictNotices.Count > 0)
                {
                    CreateConflictNoticeRows(blockRect, entry, ref blockY);
                }

                if (cameraPatchByMod.TryGetValue(modId, out var cameraPatch))
                {
                    CreateCameraPatchRow(blockRect, cameraPatch, ref blockY);
                }

                if (mouseWheelInvertByMod.TryGetValue(modId, out var wheelPatch))
                {
                    CreateMouseWheelInvertRow(blockRect, wheelPatch, ref blockY);
                }

                if (uiScaleByMod.TryGetValue(modId, out var uiScale))
                {
                    CreateUiScaleRow(blockRect, uiScale, ref blockY);
                }

                if (settingsByMod.TryGetValue(modId, out var rows))
                {
                    for (var i = 0; i < rows.Count; i++)
                    {
                        CreateSettingRow(blockRect, rows[i], ref blockY);
                    }
                }

                blockY -= 8f; // Add some bottom padding
                blockRect.sizeDelta = new Vector2(0f, -blockY);
                y += blockY - ModBlockPadding;
            }
        }

        Plugin.Instance?.Log.LogInfo(
            $"Crabtya mods window refreshed: mods={mods.Count}, settings={settingsByMod.Values.Sum(v => v.Count)}, cameraSliders={cameraPatchByMod.Count}, pendingToggles={pendingToggles.Count}, contentY={y:0.0}, templateButton={(_nativeButtonTemplate is not null)}, templateSlider={(_nativeSliderTemplate is not null)}.");

        // Expand content list to fit all rows, then reset scroll to top (or restore saved position).
        if (_scrollContent is not null)
        {
            var totalHeight = Math.Max(50f, -y + ModBlockPadding);
            _scrollContent.sizeDelta = new Vector2(0f, totalHeight);
        }

        if (_scrollRect is not null)
        {
            try
            {
                _scrollRect.verticalNormalizedPosition = scrollPosition ?? 1f;
            }
            catch
            {
            }
        }

        // Update hint text based on how many restart-required changes are queued.
        UpdateHintText(pendingToggles.Count);
    }

    private static void CreateModHeader(
        RectTransform parent,
        string modId,
        ModStateEntry entry,
        Dictionary<string, bool> pendingToggles,
        string startupQueuePath,
        int modRowIndex,
        ref float y)
    {
        var row = new GameObject($"Mod.{modId}");
        row.transform.SetParent(parent, false);
        var rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0f, 1f);
        rowRect.anchoredPosition = new Vector2(0f, y);
        rowRect.sizeDelta = new Vector2(0f, HeaderRowHeight);
        var rowImage = row.AddComponent<Image>();
        rowImage.color = Color.clear;
        rowImage.raycastTarget = false;

        var nameLabel = new GameObject("Name");
        nameLabel.transform.SetParent(row.transform, false);
        var nameRect = nameLabel.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0.42f);
        nameRect.anchorMax = new Vector2(0.65f, 1f);
        nameRect.offsetMin = new Vector2(24f, 0f);
        nameRect.offsetMax = new Vector2(0f, -8f);
        var nameText = AddTmp(nameLabel, string.Empty, 26, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        nameText.color = _textColor;
        nameText.raycastTarget = false;
        var displayName = string.IsNullOrWhiteSpace(entry.Name) ? modId : entry.Name;
        nameText.text = string.IsNullOrWhiteSpace(entry.Version) ? displayName : $"{displayName}  <size=16>v{entry.Version}</size>";

        var subLabel = new GameObject("Sub");
        subLabel.transform.SetParent(row.transform, false);
        var subRect = subLabel.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 0f);
        subRect.anchorMax = new Vector2(0.65f, 0.42f);
        subRect.offsetMin = new Vector2(24f, 4f);
        subRect.offsetMax = new Vector2(0f, 0f);
        var subText = AddTmp(subLabel, string.Empty, 16, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        subText.color = _textMutedColor;
        subText.raycastTarget = false;
        var authorPart = string.IsNullOrWhiteSpace(entry.Author) ? string.Empty : $"by {entry.Author}  ·  ";
        subText.text = $"{authorPart}{entry.Status}".Trim();

        var pending = pendingToggles.TryGetValue(modId, out var pendingEnabled);
        var displayedEnabled = pending ? pendingEnabled : entry.Enabled;

        ButtonBinding? toggleBinding = null;
        try
        {
            toggleBinding = CreateButtonBinding(
                row.transform,
                "ToggleEnabled",
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-20f, 0f),
                new Vector2(200f, 50f),
                BuildToggleLabel(displayedEnabled, pending),
                displayedEnabled ? _enabledColor : _disabledColor,
                _headerTextColor,
                18f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);
        }
        catch (Exception ex)
        {
            Plugin.Instance?.Log.LogWarning($"Crabtya mods window: toggle button creation failed for {modId}: {ex.Message}");
        }

        if (toggleBinding is not null)
        {
            var capturedModId = modId;
            var capturedEnabledNow = entry.Enabled;
            var localEnabled = displayedEnabled;
            var localPending = pending;
            var capturedBinding = toggleBinding;
            var capturedRequiresRestart = LiveModRegistry.RequiresRestart(modId);
            toggleBinding.Button.onClick.AddListener((Action)(() =>
            {
                try
                {
                    localEnabled = !localEnabled;
                    var hotApplied = false;

                    if (!capturedRequiresRestart)
                    {
                        // Content-only mod: attempt instant apply without queuing a restart.
                        if (LiveModRegistry.TryHotToggle(capturedModId, localEnabled, out var hotErr))
                        {
                            hotApplied = true;
                            localPending = false;
                            // Clear any stale queue entry from a previous session.
                            StartupCommandQueue.RemoveToggle(startupQueuePath, capturedModId, out _, out _);
                            Plugin.Instance?.Log.LogInfo(
                                $"Crabtya mods window: live-toggled enable={localEnabled} for {capturedModId}.");
                            // Rebuild the window so conflict notice rows reflect the updated state.
                            float? savedScroll = null;
                            try { savedScroll = _scrollRect?.verticalNormalizedPosition; } catch { }
                            Refresh(savedScroll);
                            return;
                        }
                        else
                        {
                            Plugin.Instance?.Log.LogWarning(
                                $"Crabtya mods window: hot-toggle failed, falling back to restart queue for {capturedModId}: {hotErr}");
                        }
                    }

                    if (!hotApplied)
                    {
                        localPending = localEnabled != capturedEnabledNow;
                        if (!localPending)
                        {
                            StartupCommandQueue.RemoveToggle(startupQueuePath, capturedModId, out _, out _);
                        }
                        else
                        {
                            StartupCommandQueue.EnqueueToggle(
                                startupQueuePath,
                                new ModToggleCommand(capturedModId, localEnabled, "mods_window"),
                                out _);
                        }

                        Plugin.Instance?.Log.LogInfo(
                            $"Crabtya mods window: queued enable={localEnabled} for {capturedModId}.");
                    }

                    capturedBinding.SetFaceColor(localEnabled ? _enabledColor : _disabledColor);
                    capturedBinding.SetLabel(BuildToggleLabel(localEnabled, localPending));
                }
                catch (Exception ex)
                {
                    Plugin.Instance?.Log.LogWarning(
                        $"Crabtya mods window enable toggle failed for {capturedModId}: {ex.Message}");
                }
            }));
        }

        y -= HeaderRowHeight + 2f;
    }

    private static string BuildToggleLabel(bool enabled, bool pending)
    {
        var core = enabled ? "ENABLED" : "DISABLED";
        return pending ? $"{core} (restart)" : core;
    }

    private static void CreateConflictNoticeRows(RectTransform parent, ModStateEntry entry, ref float y)
    {
        var isBlocked = string.Equals(entry.ConflictStatus, "blocked", StringComparison.OrdinalIgnoreCase);
        var noticeColor = isBlocked
            ? new Color(0.55f, 0.20f, 0.16f, 1f)  // dark red for blocked
            : new Color(0.50f, 0.35f, 0.08f, 1f);  // amber-brown for warning

        foreach (var notice in entry.ConflictNotices)
        {
            var prefix = isBlocked ? "[!] " : "[*] ";
            var row = new GameObject("ConflictNotice");
            row.transform.SetParent(parent, false);
            var rowRect = row.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, y);
            rowRect.sizeDelta = new Vector2(0f, 36f);
            var label = AddTmp(row, $"{prefix}{notice}", 14, TextAlignmentOptions.MidlineLeft, FontStyles.Italic);
            label.color = noticeColor;
            label.raycastTarget = false;
            label.margin = new Vector4(24f, 0f, 12f, 0f);
            y -= 38f;
        }
    }

    private static void CreateInfoLabel(RectTransform parent, string text, ref float y)
    {
        var row = new GameObject("Info");
        row.transform.SetParent(parent, false);
        var rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0f, 1f);
        rowRect.anchoredPosition = new Vector2(0f, y);
        rowRect.sizeDelta = new Vector2(0f, 52f);
        var label = AddTmp(row, text, 24, TextAlignmentOptions.Center, FontStyles.Italic);
        label.color = _textMutedColor;
        label.raycastTarget = false;
        y -= 60f;
    }

    private static void CreateCameraPatchRow(RectTransform parent, CameraPatchDefinition patch, ref float y)
    {
        var row = NewRow(parent, $"Setting.Camera.{patch.Id}", y);
        var label = patch.Settings.Label;
        if (string.IsNullOrWhiteSpace(label))
        {
            label = "FOV";
        }

        CreateRowLabel(row, $"    {label}");

        var slider = CreateSlider(row, 340f, 220f, 20f);
        slider.wholeNumbers = false;
        slider.minValue = GetMin(patch);
        slider.maxValue = GetMax(patch);
        var currentValue = Mathf.Clamp(Quantize(GetCurrentValue(patch), GetStep(patch)), GetMin(patch), GetMax(patch));
        slider.SetValueWithoutNotify(currentValue);

        var valueText = CreateValueText(row, 580f, 80f);
        valueText.text = $"{currentValue.ToString("0.##", CultureInfo.InvariantCulture)}x";

        var capturedPatch = patch;
        slider.onValueChanged.AddListener((Action<float>)(v =>
        {
            try
            {
                var next = Mathf.Clamp(Quantize(v, GetStep(capturedPatch)), GetMin(capturedPatch), GetMax(capturedPatch));
                slider.SetValueWithoutNotify(next);
                valueText.text = $"{next.ToString("0.##", CultureInfo.InvariantCulture)}x";
                RuntimeModSettings.SetFloat(capturedPatch.Id, next);
                CameraPatchApplicator.ApplyToLiveCameras();
            }
            catch (Exception ex)
            {
                Plugin.Instance?.Log.LogWarning($"Crabtya mods window camera slider update failed: {ex.Message}");
            }
        }));

        y -= SettingRowHeight + 2f;
    }

    private static void CreateMouseWheelInvertRow(RectTransform parent, CameraPatchDefinition patch, ref float y)
    {
        var row = NewRow(parent, $"Setting.MouseWheelInvert.{patch.Id}", y);
        CreateRowLabel(row, "    Invert Scroll");

        var key = MouseWheelZoomApplicator.GetInvertSettingKey(patch);
        var current = RuntimeModSettings.TryGetSettingBool(key, out var existing)
            ? existing
            : patch.Settings.Invert;

        var binding = CreateButtonBinding(
            row.transform,
            "Toggle",
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(340f, 0f),
            new Vector2(140f, 38f),
            current ? "ON" : "OFF",
            current ? _enabledColor : _disabledColor,
            _headerTextColor,
            16f,
            FontStyles.Bold,
            TextAlignmentOptions.Center);

        binding.Button.onClick.AddListener((Action)(() =>
        {
            current = !current;
            RuntimeModSettings.SetSettingBool(key, current);
            binding.SetLabel(current ? "ON" : "OFF");
            binding.SetFaceColor(current ? _enabledColor : _disabledColor);
        }));

        y -= SettingRowHeight + 2f;
    }

    private static void CreateUiScaleRow(RectTransform parent, UiScaleDefinition patch, ref float y)
    {
        var row = NewRow(parent, $"Setting.UiScale.{patch.Id}", y);
        var label = patch.Settings.Label;
        if (string.IsNullOrWhiteSpace(label))
        {
            label = "UI Scale";
        }

        CreateRowLabel(row, $"    {label}");

        var slider = CreateSlider(row, 340f, 220f, 20f);
        slider.wholeNumbers = false;
        slider.minValue = UiScaleApplicator.GetMin(patch);
        slider.maxValue = UiScaleApplicator.GetMax(patch);
        var currentValue = Mathf.Clamp(Quantize(UiScaleApplicator.GetCurrentScale(patch), UiScaleApplicator.GetStep(patch)), UiScaleApplicator.GetMin(patch), UiScaleApplicator.GetMax(patch));
        slider.SetValueWithoutNotify(currentValue);

        var valueText = CreateValueText(row, 580f, 80f);
        valueText.text = $"{currentValue.ToString("0.##", CultureInfo.InvariantCulture)}x";

        var capturedPatch = patch;
        slider.onValueChanged.AddListener((Action<float>)(v =>
        {
            try
            {
                var next = Mathf.Clamp(Quantize(v, UiScaleApplicator.GetStep(capturedPatch)), UiScaleApplicator.GetMin(capturedPatch), UiScaleApplicator.GetMax(capturedPatch));
                slider.SetValueWithoutNotify(next);
                valueText.text = $"{next.ToString("0.##", CultureInfo.InvariantCulture)}x";
                RuntimeModSettings.SetFloat(capturedPatch.Id, next);
                UiScaleApplicator.ApplyToLiveUi();
            }
            catch (Exception ex)
            {
                Plugin.Instance?.Log.LogWarning($"Crabtya mods window ui scale slider update failed: {ex.Message}");
            }
        }));

        y -= SettingRowHeight + 2f;
    }
    private static void CreateSettingRow(RectTransform parent, ModSettingRegistration registration, ref float y)
    {
        var row = NewRow(parent, $"Setting.{registration.StorageKey}", y);
        CreateRowLabel(row, $"    {registration.Definition.Label}");

        switch (registration.Definition.Kind)
        {
            case CrabtyaSettingKind.Bool:
                CreateBoolControl(row, registration);
                break;
            case CrabtyaSettingKind.Int:
                CreateIntControl(row, registration);
                break;
            case CrabtyaSettingKind.Float:
                CreateFloatControl(row, registration);
                break;
            case CrabtyaSettingKind.Option:
                CreateOptionControl(row, registration);
                break;
            default:
                Plugin.Instance?.Log.LogWarning($"Crabtya mods window: unsupported setting kind {registration.Definition.Kind} for {registration.StorageKey}.");
                break;
        }

        y -= SettingRowHeight + 2f;
    }

    private static RectTransform NewRow(RectTransform parent, string name, float y)
    {
        var row = new GameObject(name);
        row.transform.SetParent(parent, false);
        var rect = row.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(0f, SettingRowHeight);
        return rect;
    }

    private static void CreateRowLabel(RectTransform row, string textValue)
    {
        var label = new GameObject("Label");
        label.transform.SetParent(row, false);
        var rect = label.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(24f, 0f);
        rect.sizeDelta = new Vector2(320f, SettingRowHeight);
        var text = AddTmp(label, textValue, 17, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        text.color = _textColor;
        text.raycastTarget = false;
    }

    private static void CreateBoolControl(RectTransform row, ModSettingRegistration registration)
    {
        var value = CrabtyaSettingsRegistry.TryGetBool(registration.StorageKey, out var existing)
            ? existing
            : registration.Definition.DefaultValue is bool d && d;
        var binding = CreateButtonBinding(
            row.transform,
            "Toggle",
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(340f, 0f),
            new Vector2(140f, 38f),
            value ? "ON" : "OFF",
            value ? _enabledColor : _disabledColor,
            _headerTextColor,
            16f,
            FontStyles.Bold,
            TextAlignmentOptions.Center);

        var captured = registration;
        var current = value;
        binding.Button.onClick.AddListener((Action)(() =>
        {
            current = !current;
            RuntimeModSettings.SetSettingBool(captured.StorageKey, current);
            binding.SetLabel(current ? "ON" : "OFF");
            binding.SetFaceColor(current ? _enabledColor : _disabledColor);
        }));
    }

    private static void CreateIntControl(RectTransform row, ModSettingRegistration registration)
    {
        var slider = CreateSlider(row, 340f, 220f, 20f);
        var min = registration.Definition.IntMin ?? 0;
        var max = registration.Definition.IntMax ?? 100;
        var step = Math.Max(1, registration.Definition.IntStep ?? 1);
        slider.wholeNumbers = false;
        slider.minValue = min;
        slider.maxValue = max;
        var value = CrabtyaSettingsRegistry.TryGetInt(registration.StorageKey, out var existing)
            ? existing
            : registration.Definition.DefaultValue is int d ? d : min;
        slider.SetValueWithoutNotify(value);

        var valueText = CreateValueText(row, 580f, 80f);
        valueText.text = value.ToString(CultureInfo.InvariantCulture);

        var captured = registration;
        slider.onValueChanged.AddListener((Action<float>)(v =>
        {
            var next = (int)Math.Round(v / step) * step;
            next = Math.Clamp(next, min, max);
            slider.SetValueWithoutNotify(next);
            valueText.text = next.ToString(CultureInfo.InvariantCulture);
            RuntimeModSettings.SetSettingInt(captured.StorageKey, next);
        }));
    }

    private static void CreateFloatControl(RectTransform row, ModSettingRegistration registration)
    {
        var slider = CreateSlider(row, 340f, 220f, 20f);
        var min = registration.Definition.FloatMin ?? 0f;
        var max = registration.Definition.FloatMax ?? 2f;
        var step = Math.Max(0.001f, registration.Definition.FloatStep ?? 0.05f);
        slider.wholeNumbers = false;
        slider.minValue = min;
        slider.maxValue = max;
        var value = CrabtyaSettingsRegistry.TryGetFloat(registration.StorageKey, out var existing)
            ? existing
            : registration.Definition.DefaultValue is float d ? d : min;
        slider.SetValueWithoutNotify(value);

        var valueText = CreateValueText(row, 580f, 80f);
        valueText.text = value.ToString("0.##", CultureInfo.InvariantCulture);

        var captured = registration;
        slider.onValueChanged.AddListener((Action<float>)(v =>
        {
            var next = (float)Math.Round(v / step) * step;
            next = Math.Clamp(next, min, max);
            slider.SetValueWithoutNotify(next);
            valueText.text = next.ToString("0.##", CultureInfo.InvariantCulture);
            RuntimeModSettings.SetSettingFloat(captured.StorageKey, next);
        }));
    }

    private static void CreateOptionControl(RectTransform row, ModSettingRegistration registration)
    {
        var options = registration.Definition.Options.Count == 0
            ? new List<string> { registration.Definition.DefaultValue?.ToString() ?? "Option" }
            : registration.Definition.Options.ToList();
        var current = CrabtyaSettingsRegistry.TryGetOption(registration.StorageKey, out var existing)
            ? existing
            : registration.Definition.DefaultValue?.ToString() ?? options[0];
        var index = Math.Max(0, options.FindIndex(v => string.Equals(v, current, StringComparison.OrdinalIgnoreCase)));

        var binding = CreateButtonBinding(
            row.transform,
            "Option",
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(340f, 0f),
            new Vector2(260f, 38f),
            options[index],
            _optionBgColor,
            _textColor,
            15f,
            FontStyles.Bold,
            TextAlignmentOptions.Center);

        var captured = registration;
        binding.Button.onClick.AddListener((Action)(() =>
        {
            index = (index + 1) % options.Count;
            var next = options[index];
            RuntimeModSettings.SetSettingOption(captured.StorageKey, next);
            binding.SetLabel(next);
        }));
    }

    private static Slider CreateSlider(RectTransform parent, float x, float width, float height)
    {
        if (_nativeSliderTemplate is not null && IsUnityObjectAlive(_nativeSliderTemplate))
        {
            try
            {
                var clone = UnityEngine.Object.Instantiate(_nativeSliderTemplate.gameObject, parent);
                clone.name = "Slider";
                clone.SetActive(true);
                StripLocalizationComponents(clone);
                StripNonUnityComponents(clone);
                var sliderRect = clone.GetComponent<RectTransform>();
                if (sliderRect is not null)
                {
                    sliderRect.anchorMin = new Vector2(0f, 0.5f);
                    sliderRect.anchorMax = new Vector2(0f, 0.5f);
                    sliderRect.pivot = new Vector2(0f, 0.5f);
                    sliderRect.anchoredPosition = new Vector2(x, 0f);
                    sliderRect.sizeDelta = new Vector2(width, height);
                }

                var slider = clone.GetComponent<Slider>();
                if (slider is not null)
                {
                    slider.onValueChanged.RemoveAllListeners();
                    slider.navigation = new Navigation { mode = Navigation.Mode.None };
                    return slider;
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance?.Log.LogWarning($"Crabtya mods window: native slider clone failed, using fallback slider: {ex.Message}");
                _nativeSliderTemplate = null;
            }
        }

        return CreateFallbackSlider(parent, x, width, height);
    }

    private static Slider CreateFallbackSlider(RectTransform parent, float x, float width, float height)
    {
        var sliderObject = new GameObject("Slider");
        sliderObject.transform.SetParent(parent, false);
        var sliderRect = sliderObject.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0.5f);
        sliderRect.anchorMax = new Vector2(0f, 0.5f);
        sliderRect.pivot = new Vector2(0f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(x, 0f);
        sliderRect.sizeDelta = new Vector2(width, height);

        var backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(sliderObject.transform, false);
        var backgroundRect = backgroundObject.AddComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.25f);
        backgroundRect.anchorMax = new Vector2(1f, 0.75f);
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        var backgroundImage = backgroundObject.AddComponent<Image>();
        backgroundImage.color = _sliderBgColor;

        var fillAreaObject = new GameObject("Fill Area");
        fillAreaObject.transform.SetParent(sliderObject.transform, false);
        var fillAreaRect = fillAreaObject.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0f);
        fillAreaRect.anchorMax = new Vector2(1f, 1f);
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        var fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(fillAreaObject.transform, false);
        var fillRect = fillObject.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0.25f);
        fillRect.anchorMax = new Vector2(1f, 0.75f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImage = fillObject.AddComponent<Image>();
        fillImage.color = _sliderFillColor;

        var handleAreaObject = new GameObject("Handle Slide Area");
        handleAreaObject.transform.SetParent(sliderObject.transform, false);
        var handleAreaRect = handleAreaObject.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(8f, 0f);
        handleAreaRect.offsetMax = new Vector2(-8f, 0f);

        var handleObject = new GameObject("Handle");
        handleObject.transform.SetParent(handleAreaObject.transform, false);
        var handleRect = handleObject.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(14f, 22f);
        var handleImage = handleObject.AddComponent<Image>();
        handleImage.color = _sliderHandleColor;

        var slider = sliderObject.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.wholeNumbers = false;
        return slider;
    }

    private static TextMeshProUGUI CreateValueText(RectTransform parent, float x, float width)
    {
        var valueObject = new GameObject("ValueText");
        valueObject.transform.SetParent(parent, false);
        var valueRect = valueObject.AddComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(0f, 0.5f);
        valueRect.anchorMax = new Vector2(0f, 0.5f);
        valueRect.pivot = new Vector2(0f, 0.5f);
        valueRect.anchoredPosition = new Vector2(x, 0f);
        valueRect.sizeDelta = new Vector2(width, 24f);

        var text = AddTmp(valueObject, string.Empty, 16, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        text.color = _textColor;
        text.raycastTarget = false;
        return text;
    }

    private static ButtonBinding CreateButtonBinding(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        string label,
        Color faceColor,
        Color textColor,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        GameObject host;
        if (_nativeButtonTemplate is not null && IsUnityObjectAlive(_nativeButtonTemplate))
        {
            try
            {
                host = UnityEngine.Object.Instantiate(_nativeButtonTemplate, parent);
                StripLocalizationComponents(host);
                StripNonUnityComponents(host);
                StripLayoutComponents(host);
            }
            catch
            {
                // Template became invalid mid-frame; fall through to manual creation.
                _nativeButtonTemplate = null;
                host = new GameObject(name);
                host.transform.SetParent(parent, false);
                host.AddComponent<Image>();
            }
        }
        else
        {
            host = new GameObject(name);
            host.transform.SetParent(parent, false);
            host.AddComponent<Image>();
        }

        host.name = name;
        host.SetActive(true);
        var hostRect = host.GetComponent<RectTransform>() ?? host.AddComponent<RectTransform>();
        hostRect.anchorMin = anchorMin;
        hostRect.anchorMax = anchorMax;
        hostRect.pivot = pivot;
        hostRect.anchoredPosition = anchoredPosition;
        hostRect.sizeDelta = sizeDelta;
        hostRect.localScale = Vector3.one;
        hostRect.localRotation = Quaternion.identity;

        var rootImage = host.GetComponent<Image>() ?? host.GetComponentInChildren<Image>(true);
        if (rootImage is null)
        {
            rootImage = host.AddComponent<Image>();
        }

        _pixelPanelSprite ??= CreateRoundedPixelSprite(size: 48, cornerRadius: 0, borderInset: 0);
        rootImage.sprite = _pixelPanelSprite;
        rootImage.type = Image.Type.Sliced;

        rootImage.color = faceColor;
        DisableChildRaycasts(host, null);

        // Add thick border ring around the button for native pixel-art definition.
        AddBorder(host.transform, 4f, _panelBorderColor);
        // Inner highlight
        AddBorderEdge(host.transform, "Border.Inner.Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-8f, 2f), new Vector2(0f, -4f), new Color(0.86f, 0.78f, 0.63f, 1f));
        AddBorderEdge(host.transform, "Border.Inner.Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-8f, 2f), new Vector2(0f, 4f), new Color(0.86f, 0.78f, 0.63f, 1f));
        AddBorderEdge(host.transform, "Border.Inner.Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(2f, -8f), new Vector2(4f, 0f), new Color(0.86f, 0.78f, 0.63f, 1f));
        AddBorderEdge(host.transform, "Border.Inner.Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(2f, -8f), new Vector2(-4f, 0f), new Color(0.86f, 0.78f, 0.63f, 1f));

        MuteTemplateTextObjects(host);

        const string ForcedLabelName = "Crabtya.Button.Label";
        var forcedLabelTransform = host.transform.Find(ForcedLabelName);
        TextMeshProUGUI forcedLabel;
        if (forcedLabelTransform is null)
        {
            var textHost = new GameObject(ForcedLabelName);
            textHost.transform.SetParent(host.transform, false);
            var textRect = textHost.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            forcedLabel = AddTmp(textHost, string.Empty, fontSize, alignment, style);
        }
        else
        {
            forcedLabel = forcedLabelTransform.GetComponent<TextMeshProUGUI>() ?? AddTmp(forcedLabelTransform.gameObject, string.Empty, fontSize, alignment, style);
        }

        forcedLabel.enabled = true;
        forcedLabel.raycastTarget = false;
        var labelTargets = new List<TMP_Text> { forcedLabel };
        var legacyTargets = new List<Text>();
        SetTextTargets(labelTargets, legacyTargets, label, textColor, fontSize, style, alignment);

        var overlay = host.transform.Find(ClickInterceptorName)?.gameObject;
        if (overlay is null)
        {
            overlay = new GameObject(ClickInterceptorName);
            overlay.transform.SetParent(host.transform, false);
            var overlayRect = overlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            var overlayImage = overlay.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0f);
            overlayImage.raycastTarget = true;
            overlay.AddComponent<Button>();
        }

        overlay.transform.SetAsLastSibling();
        var overlayImageComponent = overlay.GetComponent<Image>();
        if (overlayImageComponent is not null)
        {
            overlayImageComponent.raycastTarget = true;
        }

        var button = overlay.GetComponent<Button>() ?? overlay.AddComponent<Button>();
        button.onClick.RemoveAllListeners();

        return new ButtonBinding(
            button,
            () => rootImage,
            v => SetTextTargets(labelTargets, legacyTargets, v, textColor, fontSize, style, alignment));
    }

    private static void SetTextTargets(
        IReadOnlyList<TMP_Text> tmpTargets,
        IReadOnlyList<Text> legacyTargets,
        string value,
        Color textColor,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        for (var i = 0; i < tmpTargets.Count; i++)
        {
            var target = tmpTargets[i];
            if (target is null)
            {
                continue;
            }

            target.text = value;
            target.color = textColor;
            target.fontSize = fontSize;
            target.fontStyle = style;
            target.alignment = alignment;
            target.raycastTarget = false;
            target.enableWordWrapping = false;
            target.enableAutoSizing = false;
            target.margin = new Vector4(4f, 0f, 4f, 0f);
            target.overflowMode = TextOverflowModes.Truncate;
            target.characterSpacing = 0f;
            target.wordSpacing = 0f;
            target.lineSpacing = 1f;

            var rect = target.GetComponent<RectTransform>();
            if (rect is not null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;
            }
        }

        for (var i = 0; i < legacyTargets.Count; i++)
        {
            var target = legacyTargets[i];
            if (target is null)
            {
                continue;
            }

            target.text = value;
            target.color = textColor;
            target.fontSize = Mathf.RoundToInt(fontSize);
            target.raycastTarget = false;
            target.horizontalOverflow = HorizontalWrapMode.Overflow;
            target.verticalOverflow = VerticalWrapMode.Truncate;
            target.alignment = alignment switch
            {
                TextAlignmentOptions.Center => TextAnchor.MiddleCenter,
                TextAlignmentOptions.MidlineLeft => TextAnchor.MiddleLeft,
                TextAlignmentOptions.MidlineRight => TextAnchor.MiddleRight,
                _ => TextAnchor.MiddleCenter
            };

            var rect = target.GetComponent<RectTransform>();
            if (rect is not null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;
            }
        }
    }

    private static void MuteTemplateTextObjects(GameObject host)
    {
        var tmpTargets = host.GetComponentsInChildren<TMP_Text>(true);
        for (var i = 0; i < tmpTargets.Length; i++)
        {
            var target = tmpTargets[i];
            if (target is null)
            {
                continue;
            }

            target.text = string.Empty;
            target.enabled = false;
        }

        var legacyTargets = host.GetComponentsInChildren<Text>(true);
        for (var i = 0; i < legacyTargets.Length; i++)
        {
            var target = legacyTargets[i];
            if (target is null)
            {
                continue;
            }

            target.text = string.Empty;
            target.enabled = false;
        }
    }

    private static void DisableChildRaycasts(GameObject root, GameObject? except)
    {
        var graphics = root.GetComponentsInChildren<Graphic>(true);
        for (var i = 0; i < graphics.Length; i++)
        {
            var graphic = graphics[i];
            if (graphic is null)
            {
                continue;
            }

            if (except is not null && graphic.gameObject == except)
            {
                continue;
            }

            graphic.raycastTarget = false;
        }
    }

    private static void StripLocalizationComponents(GameObject root)
    {
        var components = root.GetComponentsInChildren<Component>(true);
        for (var i = 0; i < components.Length; i++)
        {
            var component = components[i];
            if (component is null)
            {
                continue;
            }

            var typeName = component.GetType().FullName ?? string.Empty;
            if (!typeName.Contains("Localiz", StringComparison.OrdinalIgnoreCase) &&
                !typeName.Contains("I2", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                UnityEngine.Object.Destroy(component);
            }
            catch
            {
            }
        }
    }

    private static void StripNonUnityComponents(GameObject root)
    {
        var components = root.GetComponentsInChildren<Component>(true);
        for (var i = 0; i < components.Length; i++)
        {
            var component = components[i];
            if (component is null)
            {
                continue;
            }

            if (component is Transform)
            {
                continue;
            }

            var ns = component.GetType().Namespace ?? string.Empty;
            if (ns.StartsWith("UnityEngine", StringComparison.Ordinal) || ns.StartsWith("TMPro", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                UnityEngine.Object.Destroy(component);
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// Strips LayoutGroup, ContentSizeFitter, and LayoutElement components from a cloned
    /// button template. These fight with manual RectTransform positioning and cause text
    /// misalignment when a button is repurposed outside its original layout context.
    /// </summary>
    private static void StripLayoutComponents(GameObject root)
    {
        var layouts = root.GetComponentsInChildren<LayoutGroup>(true);
        for (var i = 0; i < layouts.Length; i++)
        {
            if (layouts[i] is not null)
            {
                try { UnityEngine.Object.Destroy(layouts[i]); } catch { }
            }
        }

        var fitters = root.GetComponentsInChildren<ContentSizeFitter>(true);
        for (var i = 0; i < fitters.Length; i++)
        {
            if (fitters[i] is not null)
            {
                try { UnityEngine.Object.Destroy(fitters[i]); } catch { }
            }
        }

        var elements = root.GetComponentsInChildren<LayoutElement>(true);
        for (var i = 0; i < elements.Length; i++)
        {
            if (elements[i] is not null)
            {
                try { UnityEngine.Object.Destroy(elements[i]); } catch { }
            }
        }
    }

    private static void ResolveThemeAndTemplates()
    {
        if (_themeResolved && AreTemplatesAlive())
        {
            return;
        }

        // If previously resolved but templates became stale (scene change), clear and re-resolve.
        if (_themeResolved)
        {
            _nativeButtonTemplate = null;
            _nativeButtonSprite = null;
            _nativeSliderTemplate = null;
            _themeResolved = false;
        }

        ResolveNativeTemplates();
        DiscoverNativeFont();
        ApplyThemeFromTemplates();
        _themeResolved = true;

        if (_themeLogWritten)
        {
            return;
        }

        _themeLogWritten = true;
        Plugin.Instance?.Log.LogInfo(
            $"Crabtya mods window theme resolved: buttonTemplate={(_nativeButtonTemplate is not null)}, buttonSprite={(_nativeButtonSprite is not null)}, sliderTemplate={(_nativeSliderTemplate is not null)}, font={(_nativeFont is not null ? _nativeFont.name : "<fallback>")}.");
    }

    /// <summary>
    /// Checks whether cached template Unity objects are still alive (not destroyed).
    /// In IL2CPP, a C# reference can be non-null while the native Unity object is destroyed.
    /// </summary>
    private static bool AreTemplatesAlive()
    {
        try
        {
            if (_nativeButtonTemplate is not null)
            {
                // Unity operator== returns true when comparing to null for destroyed objects.
                if ((UnityEngine.Object)_nativeButtonTemplate == null)
                {
                    return false;
                }
            }

            if (_nativeSliderTemplate is not null)
            {
                if ((UnityEngine.Object)_nativeSliderTemplate == null)
                {
                    return false;
                }
            }
        }
        catch
        {
            return false;
        }

        return true;
    }

    private static void ResolveNativeTemplates()
    {
        try
        {
            var menus = Resources.FindObjectsOfTypeAll<MainMenuScreenUI>();
            for (var i = 0; i < menus.Length; i++)
            {
                var menu = menus[i];
                if (menu is null || menu.gameObject is null || !menu.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (menu._settingsButton is not null && menu._settingsButton.gameObject is not null)
                {
                    _nativeButtonTemplate = menu._settingsButton.gameObject;
                    var buttonImage = _nativeButtonTemplate.GetComponent<Image>() ?? _nativeButtonTemplate.GetComponentInChildren<Image>(true);
                    if (buttonImage is not null)
                    {
                        _nativeButtonSprite = buttonImage.sprite;
                        if (_nativeButtonSprite is not null)
                        {
                            ForcePointFiltering(_nativeButtonSprite.texture);
                        }
                    }

                    break;
                }
            }
        }
        catch
        {
        }

        try
        {
            var panels = Resources.FindObjectsOfTypeAll<SettingsPanel>();
            for (var i = 0; i < panels.Length; i++)
            {
                var panel = panels[i];
                if (panel is null || panel.gameObject is null || !panel.gameObject.activeInHierarchy)
                {
                    continue;
                }

                RectTransform? pageRect = null;
                try
                {
                    var pages = panel._pages;
                    if (pages is not null && pages.Length > 0)
                    {
                        var pageIndex = Mathf.Clamp(panel._pageIndex, 0, pages.Length - 1);
                        var page = pages[pageIndex];
                        if (page is not null && page.gameObject is not null && page.gameObject.activeInHierarchy)
                        {
                            pageRect = page.GetComponent<RectTransform>();
                        }
                    }
                }
                catch
                {
                }

                pageRect ??= panel.GetComponent<RectTransform>();
                if (pageRect is null)
                {
                    continue;
                }

                var sliders = pageRect.GetComponentsInChildren<Slider>(true);
                for (var s = 0; s < sliders.Length; s++)
                {
                    var slider = sliders[s];
                    if (slider is null || slider.gameObject is null || IsLoaderObject(slider.transform))
                    {
                        continue;
                    }

                    _nativeSliderTemplate = slider;
                    if (_nativeSliderTemplate.fillRect is not null)
                    {
                        var fillImage = _nativeSliderTemplate.fillRect.GetComponent<Image>();
                        if (fillImage is not null && fillImage.sprite is not null)
                        {
                            ForcePointFiltering(fillImage.sprite.texture);
                        }
                    }

                    if (_nativeSliderTemplate.handleRect is not null)
                    {
                        var handleImage = _nativeSliderTemplate.handleRect.GetComponent<Image>();
                        if (handleImage is not null && handleImage.sprite is not null)
                        {
                            ForcePointFiltering(handleImage.sprite.texture);
                        }
                    }

                    break;
                }

                if (_nativeSliderTemplate is not null)
                {
                    break;
                }
            }
        }
        catch
        {
        }
    }

    private static void ApplyThemeFromTemplates()
    {
        var hasNativeButtonTheme = false;
        if (_nativeButtonTemplate is not null)
        {
            var buttonImage = _nativeButtonTemplate.GetComponent<Image>() ?? _nativeButtonTemplate.GetComponentInChildren<Image>(true);
            if (buttonImage is not null)
            {
                hasNativeButtonTheme = true;
                _closeBgColor = buttonImage.color;
                _optionBgColor = buttonImage.color;
                _panelBorderColor = ScaleColor(buttonImage.color, 0.65f, 1f);
            }

            var buttonText = _nativeButtonTemplate.GetComponentInChildren<TMP_Text>(true);
            if (buttonText is not null)
            {
                _headerTextColor = buttonText.color;
                _textColor = ScaleColor(buttonText.color, 0.75f, 1f);
                _textMutedColor = ScaleColor(buttonText.color, 0.6f, 0.95f);
            }
        }

        if (_nativeSliderTemplate is not null)
        {
            if (_nativeSliderTemplate.fillRect is not null)
            {
                var fillImage = _nativeSliderTemplate.fillRect.GetComponent<Image>();
                if (fillImage is not null)
                {
                    _sliderFillColor = fillImage.color;
                }
            }

            if (_nativeSliderTemplate.handleRect is not null)
            {
                var handleImage = _nativeSliderTemplate.handleRect.GetComponent<Image>();
                if (handleImage is not null)
                {
                    _sliderHandleColor = handleImage.color;
                }
            }

            var backgroundImage = _nativeSliderTemplate.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(i => i is not null && i.transform != _nativeSliderTemplate.fillRect && i.transform != _nativeSliderTemplate.handleRect);
            if (backgroundImage is not null)
            {
                _sliderBgColor = backgroundImage.color;
            }
        }

        // Keep the menu warm and game-native; use saturated earthy tones matching
        // the pixel-art wooden style visible in the game's main menu and pause menu.
        var warmBase = hasNativeButtonTheme
            ? Color.Lerp(_optionBgColor, new Color(0.85f, 0.72f, 0.52f, 1f), 0.50f)
            : new Color(0.85f, 0.72f, 0.52f, 1f);
        _panelColor = new Color(0.92f, 0.82f, 0.57f, 1f); // Match pause menu BG
        _panelBorderColor = new Color(0.23f, 0.14f, 0.07f, 1f); // Dark outline
        _rowBgColor = new Color(0.86f, 0.76f, 0.51f, 1f); // Mod row background (slightly darker than panel)
        _rowAltBgColor = _rowBgColor; 
        _textColor = new Color(0.32f, 0.19f, 0.10f, 1f); // Match pause menu text
        _headerTextColor = new Color(0.92f, 0.82f, 0.57f, 1f); // Button text (match panel color for contrast)
        _textMutedColor = new Color(0.48f, 0.35f, 0.25f, 1f); // Muted brown
        _enabledColor = new Color(0.38f, 0.53f, 0.34f, 1f); // Earthy green
        _disabledColor = new Color(0.70f, 0.38f, 0.33f, 1f); // Earthy red
        _closeBgColor = new Color(0.63f, 0.47f, 0.33f, 1f); // Match pause menu button
        _optionBgColor = new Color(0.63f, 0.47f, 0.33f, 1f); // Match pause menu button
        _sliderBgColor = new Color(0.63f, 0.47f, 0.33f, 1f); // Match pause menu button
        _sliderFillColor = new Color(0.86f, 0.78f, 0.63f, 1f); // Match pause menu button inner border
        _sliderHandleColor = new Color(0.97f, 0.91f, 0.81f, 1f); // Light cream
        _backdropColor = new Color(0f, 0f, 0f, 0.80f);
    }

    private static Color ScaleColor(Color color, float rgbScale, float alpha)
    {
        return new Color(
            Mathf.Clamp01(color.r * rgbScale),
            Mathf.Clamp01(color.g * rgbScale),
            Mathf.Clamp01(color.b * rgbScale),
            Mathf.Clamp01(alpha));
    }

    private static bool IsUnityObjectAlive(UnityEngine.Object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        try
        {
            return (UnityEngine.Object)obj != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsLoaderObject(Transform transform)
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

    private static void DiscoverNativeFont()
    {
        if (_nativeFont is not null)
        {
            return;
        }

        try
        {
            if (_nativeButtonTemplate is not null)
            {
                var templateFont = _nativeButtonTemplate.GetComponentsInChildren<TMP_Text>(true)
                    .FirstOrDefault(t => t is not null && t.font is not null)?.font;
                if (templateFont is not null)
                {
                    _nativeFont = templateFont;
                    ForcePointFiltering(_nativeFont);
                    return;
                }
            }

            var tmpAssets = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            TMP_FontAsset? best = null;
            var bestScore = int.MinValue;
            for (var i = 0; i < tmpAssets.Length; i++)
            {
                var candidate = tmpAssets[i];
                if (candidate is null)
                {
                    continue;
                }

                var name = candidate.name ?? string.Empty;
                if (name.Contains("LiberationSans", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Internal", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("NotoSans", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Korean", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Japanese", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Chinese", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var score = 0;
                if (name.Contains("pixel", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("retro", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("arcade", StringComparison.OrdinalIgnoreCase))
                {
                    score += 40;
                }

                if (name.Contains("menu", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("button", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("ui", StringComparison.OrdinalIgnoreCase))
                {
                    score += 10;
                }

                if (candidate.characterTable.Count is > 30 and < 300)
                {
                    score += 6;
                }

                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                best = candidate;
            }

            if (best is not null)
            {
                _nativeFont = best;
            }

            if (_nativeFont is null && tmpAssets.Length > 0)
            {
                _nativeFont = tmpAssets[0];
            }

            ForcePointFiltering(_nativeFont);
        }
        catch (Exception ex)
        {
            Plugin.Instance?.Log.LogWarning($"Crabtya mods window: TMP font discovery failed: {ex.Message}");
        }
    }

    private static TextMeshProUGUI AddTmp(GameObject host, string text, float fontSize, TextAlignmentOptions alignment, FontStyles style)
    {
        var tmp = host.AddComponent<TextMeshProUGUI>();
        if (_nativeFont is not null)
        {
            tmp.font = _nativeFont;
        }

        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.fontStyle = style;
        tmp.color = _textColor;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Truncate;
        tmp.enableAutoSizing = false;
        tmp.characterSpacing = 0f;
        tmp.wordSpacing = 0f;
        tmp.margin = Vector4.zero;
        return tmp;
    }

    private static void ApplyPanelSpriteStyle(Image image)
    {
        // Prefer the native button sprite (9-slice wooden texture) for panel backgrounds
        // so the entire window reads as the same visual family as game menus.
        if (_nativeButtonSprite is not null && IsUnityObjectAlive(_nativeButtonSprite))
        {
            image.sprite = _nativeButtonSprite;
            image.material = null;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            return;
        }

        _pixelPanelSprite ??= CreateRoundedPixelSprite(size: 48, cornerRadius: 3, borderInset: 8);
        if (_pixelPanelSprite is null)
        {
            return;
        }

        image.sprite = _pixelPanelSprite;
        image.material = null;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
    }

    private static Sprite? CreateRoundedPixelSprite(int size, int cornerRadius, int borderInset)
    {
        try
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            var clear = new Color32(255, 255, 255, 0);
            var opaque = new Color32(255, 255, 255, 255);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var idx = y * size + x;
                    pixels[idx] = IsInsideRoundedRect(x, y, size, cornerRadius) ? opaque : clear;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                16f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(borderInset, borderInset, borderInset, borderInset));
        }
        catch
        {
            return null;
        }
    }

    private static bool IsInsideRoundedRect(int x, int y, int size, int radius)
    {
        if (radius <= 0)
        {
            return true;
        }

        var min = radius;
        var max = size - radius - 1;
        if ((x >= min && x <= max) || (y >= min && y <= max))
        {
            return true;
        }

        var cx = x < min ? min : max;
        var cy = y < min ? min : max;
        var dx = x - cx;
        var dy = y - cy;
        return dx * dx + dy * dy <= radius * radius;
    }

    private static void ForcePointFiltering(TMP_FontAsset? font)
    {
        if (font is null)
        {
            return;
        }

        try
        {
            var atlasTextures = font.atlasTextures;
            for (var i = 0; i < atlasTextures.Length; i++)
            {
                ForcePointFiltering(atlasTextures[i]);
            }
        }
        catch
        {
        }
    }

    private static void ForcePointFiltering(Texture? texture)
    {
        if (texture is null)
        {
            return;
        }

        try
        {
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
        }
        catch
        {
        }
    }

    private static void AddBorder(Transform parent, float thickness, Color color)
    {
        AddBorderEdge(parent, "Border.Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness), Vector2.zero, color);
        AddBorderEdge(parent, "Border.Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness), Vector2.zero, color);
        AddBorderEdge(parent, "Border.Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f), Vector2.zero, color);
        AddBorderEdge(parent, "Border.Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f), Vector2.zero, color);
    }

    private static void AddBorderEdge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPosition, Color color)
    {
        var edge = new GameObject(name);
        edge.transform.SetParent(parent, false);
        var rect = edge.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = anchoredPosition;
        var image = edge.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static void ResetContentRect()
    {
        if (_scrollContent is null)
        {
            return;
        }

        // Reset to top-anchored; height will be set at end of Refresh().
        _scrollContent.anchorMin = new Vector2(0f, 1f);
        _scrollContent.anchorMax = new Vector2(1f, 1f);
        _scrollContent.pivot = new Vector2(0f, 1f);
        _scrollContent.anchoredPosition = Vector2.zero;
        _scrollContent.sizeDelta = new Vector2(0f, 100f);
    }

    private static void OpenModsFolder()
    {
        try
        {
            var modsPath = Path.Combine(AppContext.BaseDirectory, "Mods");
            System.Diagnostics.Process.Start("explorer.exe", modsPath);
        }
        catch (Exception ex)
        {
            Plugin.Instance?.Log.LogWarning($"Crabtya mods window: failed to open Mods folder: {ex.Message}");
        }
    }

    private static void UpdateHintText(int pendingRestartCount)
    {
        if (_hintText is null)
        {
            return;
        }

        _hintText.text = pendingRestartCount switch
        {
            0 => "Camera/visual mods apply live.  DLL & catalog mods require restart.  Press CLOSE to dismiss.",
            1 => "1 change queued for restart.  Camera/visual mods already applied.  Press CLOSE to dismiss.",
            _ => $"{pendingRestartCount} changes queued for restart.  Camera/visual mods already applied.  Press CLOSE to dismiss."
        };
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
        return patch.Settings.Min > 0f ? patch.Settings.Min : 0.5f;
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
            return float.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        return false;
    }

    private sealed class ButtonBinding
    {
        private readonly Func<Image?> _image;
        private readonly Action<string> _setLabel;

        public ButtonBinding(Button button, Func<Image?> image, Action<string> setLabel)
        {
            Button = button;
            _image = image;
            _setLabel = setLabel;
        }

        public Button Button { get; }

        public void SetLabel(string label)
        {
            _setLabel(label);
        }

        public void SetFaceColor(Color color)
        {
            var image = _image();
            if (image is not null)
            {
                image.color = color;
            }
        }
    }
}

public sealed class CrabtyaModsSettingsWindowBehaviour : MonoBehaviour
{
    public CrabtyaModsSettingsWindowBehaviour(IntPtr ptr)
        : base(ptr)
    {
    }

    public void Update()
    {
        if (!CrabtyaModsSettingsWindow.IsVisible)
        {
            return;
        }
    }
}

