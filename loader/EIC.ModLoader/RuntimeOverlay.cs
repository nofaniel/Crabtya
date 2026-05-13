using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.UI;

namespace EIC.ModLoader;

public static class RuntimeOverlay
{
    private static bool _registered;
    private static bool _created;

    public static void EnsureCreated()
    {
        if (!_registered)
        {
            ClassInjector.RegisterTypeInIl2Cpp<RuntimeOverlayBehaviour>();
            _registered = true;
        }

        if (_created)
        {
            return;
        }

        var gameObject = new GameObject("Crabtya.MainMenuBadge");
        UnityEngine.Object.DontDestroyOnLoad(gameObject);
        gameObject.hideFlags = HideFlags.HideAndDontSave;
        gameObject.AddComponent<RuntimeOverlayBehaviour>();
        _created = true;

        Plugin.Instance?.Log.LogInfo("Crabtya main-menu badge bootstrap created.");
    }
}

public sealed class RuntimeOverlayBehaviour : MonoBehaviour
{
    private const string CanvasName = "Crabtya.Badge.Canvas";
    private const string TextName = "Crabtya.Badge.Text";

    private GameObject _canvasObject;
    private GameObject? _badgeContainer;
    private Text _badgeText;
    private float _nextTickTime;
    private float _nextUiScaleTime;

    public RuntimeOverlayBehaviour(IntPtr ptr)
        : base(ptr)
    {
    }

    public void Start()
    {
        CreateUi();
        Plugin.Instance?.Log.LogInfo("Crabtya main-menu badge started.");
    }

    public void Update()
    {
        MainMenuModsButtonApplicator.Update();
        PauseMenuModsButtonApplicator.Update();
        MouseWheelZoomApplicator.Update();
        CrabtyaNativeSettingsApplicator.Update();

        var uiScaleTickSeconds = UiScaleApplicator.GetRecommendedUpdateIntervalSeconds();
        if (Time.unscaledTime >= _nextUiScaleTime)
        {
            _nextUiScaleTime = Time.unscaledTime + uiScaleTickSeconds;
            UiScaleApplicator.ApplyToLiveUi();
        }

        if (Time.unscaledTime < _nextTickTime)
        {
            return;
        }

        _nextTickTime = Time.unscaledTime + 1f;
        IntroSkipApplicator.ApplyToLiveVideoPlayers();
        VisualPatchApplicator.ApplyToLiveObjects();
        UpdateBadgeVisibility();
    }

    public void LateUpdate()
    {
        CameraPatchApplicator.ApplyToLiveCameras();
    }

    private void CreateUi()
    {
        try
        {
            _canvasObject = new GameObject(CanvasName);
            UnityEngine.Object.DontDestroyOnLoad(_canvasObject);
            _canvasObject.transform.SetParent(transform, false);

            var canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _canvasObject.AddComponent<GraphicRaycaster>();

            // Container: top-right anchor, groups background panel and text label.
            var containerObject = new GameObject("Crabtya.Badge.Container");
            containerObject.transform.SetParent(_canvasObject.transform, false);
            var containerRect = containerObject.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(1f, 1f);
            containerRect.anchorMax = new Vector2(1f, 1f);
            containerRect.pivot = new Vector2(1f, 1f);
            containerRect.anchoredPosition = new Vector2(-12f, -12f);
            containerRect.sizeDelta = new Vector2(200f, 28f);
            _badgeContainer = containerObject;

            // Background panel: native tan/parchment color matching game UI palette.
            var bgObject = new GameObject("Crabtya.Badge.Background");
            bgObject.transform.SetParent(containerObject.transform, false);
            var bgRect = bgObject.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = bgObject.AddComponent<Image>();
            bgImage.color = new Color(0.88f, 0.78f, 0.60f, 0.88f);
            bgImage.raycastTarget = false;

            // Text label: dark brown matching game's native text color.
            var textObject = new GameObject(TextName);
            textObject.transform.SetParent(containerObject.transform, false);
            var textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6f, 0f);
            textRect.offsetMax = new Vector2(-6f, 0f);

            _badgeText = textObject.AddComponent<Text>();
            _badgeText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _badgeText.fontSize = 14;
            _badgeText.fontStyle = FontStyle.Bold;
            _badgeText.alignment = TextAnchor.MiddleCenter;
            _badgeText.color = new Color(0.24f, 0.16f, 0.10f, 0.95f);
            _badgeText.raycastTarget = false;
            _badgeText.text = $"Crabtya v{Plugin.PluginVersion}";

            UpdateBadgeVisibility();
            Plugin.Instance?.Log.LogInfo("Crabtya main-menu badge UI created.");
        }
        catch (Exception ex)
        {
            Plugin.Instance?.Log.LogWarning($"Crabtya badge UI creation failed: {ex.Message}");
        }
    }

    private void UpdateBadgeVisibility()
    {
        var show = IsMainMenuLikelyVisible();
        if (_badgeContainer is not null)
        {
            _badgeContainer.SetActive(show);
        }

        if (_badgeText is null)
        {
            return;
        }

        if (show)
        {
            _badgeText.text = $"Crabtya v{Plugin.PluginVersion}";
        }
    }

    private static bool IsMainMenuLikelyVisible()
    {
        if (IsGameplayLikelyActive())
        {
            return false;
        }

        if (MainMenuModsButtonApplicator.IsMainMenuLikelyVisible())
        {
            return true;
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
            if (component is null)
            {
                continue;
            }

            var typeName = component.GetType().FullName ?? string.Empty;
            if (typeName.Contains("MainMenu", StringComparison.OrdinalIgnoreCase) && component.gameObject.activeInHierarchy)
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
}
