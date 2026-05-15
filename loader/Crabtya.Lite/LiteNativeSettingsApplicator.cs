using System.Globalization;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace Crabtya.Lite;

public static class LiteNativeSettingsApplicator
{
    private const string ScaleSliderObjectName = "CrabtyaLite.NativeSettings.FovScaleSlider";
    private const string ScaleLabelObjectName = "CrabtyaLite.NativeSettings.FovScaleLabel";
    private const string ScaleValueObjectName = "CrabtyaLite.NativeSettings.FovScaleValue";

    private const string InvertSliderObjectName = "CrabtyaLite.NativeSettings.InvertScrollSlider";
    private const string InvertLabelObjectName = "CrabtyaLite.NativeSettings.InvertScrollLabel";
    private const string InvertValueObjectName = "CrabtyaLite.NativeSettings.InvertScrollValue";

    private const string ZoomSliderObjectName = "CrabtyaLite.NativeSettings.ScrollZoomSlider";
    private const string ZoomLabelObjectName = "CrabtyaLite.NativeSettings.ScrollZoomLabel";
    private const string ZoomValueObjectName = "CrabtyaLite.NativeSettings.ScrollZoomValue";

    private static readonly string[] SettingsHints =
    {
        "settings",
        "setting",
        "options",
        "option"
    };

    private static readonly string[] PreferredAnchorLabels =
    {
        "Display Mode",
        "Language",
        "Music",
        "SFX",
        "Global"
    };

    private static ManualLogSource? _log;
    private static bool _installLogged;
    private static bool _suppressChange;
    private static float _nextProbeTime;

    private static Slider? _scaleSlider;
    private static Text? _scaleLabel;
    private static Text? _scaleValue;

    private static Slider? _invertSlider;
    private static Text? _invertLabel;
    private static Text? _invertValue;

    private static Slider? _zoomSlider;
    private static Text? _zoomLabel;
    private static Text? _zoomValue;

    private static Transform? _sliderParent;
    private static Transform? _labelParent;

    public static bool IsVisible =>
        (_scaleSlider is not null && _scaleSlider.gameObject is not null && _scaleSlider.gameObject.activeInHierarchy) ||
        (_invertSlider is not null && _invertSlider.gameObject is not null && _invertSlider.gameObject.activeInHierarchy) ||
        (_zoomSlider is not null && _zoomSlider.gameObject is not null && _zoomSlider.gameObject.activeInHierarchy);

    public static void Install(ManualLogSource log)
    {
        _log = log;
        if (_installLogged)
        {
            return;
        }

        _installLogged = true;
        _log.LogInfo("Crabtya Lite native settings integration installed.");
    }

    public static void Update()
    {
        if (Time.unscaledTime < _nextProbeTime)
        {
            return;
        }

        _nextProbeTime = Time.unscaledTime + 0.35f;

        var target = FindSettingsTarget();
        if (target is null)
        {
            DestroyInjectedControls();
            return;
        }

        if (_scaleSlider is null ||
            _invertSlider is null ||
            _zoomSlider is null ||
            _sliderParent != target.SliderParent ||
            _labelParent != target.LabelParent)
        {
            DestroyInjectedControls();
            CreateRows(target);
        }

        SyncRows();
    }

    private static SettingsTarget? FindSettingsTarget()
    {
        var sliders = FindActiveGameSliders();
        if (sliders.Count == 0)
        {
            return null;
        }

        var sliderTemplate = sliders
            .OrderByDescending(ScoreSlider)
            .FirstOrDefault();
        if (sliderTemplate is null)
        {
            return null;
        }

        var sliderTemplateRect = sliderTemplate.GetComponent<RectTransform>();
        if (sliderTemplateRect is null || sliderTemplateRect.parent is null)
        {
            return null;
        }

        var texts = FindActiveGameTexts();
        var labelTemplate = FindLabelTemplate(texts, sliderTemplateRect);
        if (labelTemplate is null)
        {
            return null;
        }

        var labelTemplateRect = labelTemplate.GetComponent<RectTransform>();
        if (labelTemplateRect is null || labelTemplateRect.parent is null)
        {
            return null;
        }

        var sliderSpacing = EstimateSpacing(
            sliders
                .Select(s => s.GetComponent<RectTransform>())
                .Where(r => r is not null && r.parent == sliderTemplateRect.parent)
                .Select(r => r.anchoredPosition.y)
                .ToList(),
            60f);
        var labelSpacing = EstimateSpacing(
            texts
                .Select(t => t.GetComponent<RectTransform>())
                .Where(r => r is not null && r.parent == labelTemplateRect.parent)
                .Select(r => r.anchoredPosition.y)
                .ToList(),
            sliderSpacing);

        var sliderFirstRowY = ChooseInsertionY(
            sliders
                .Select(s => s.GetComponent<RectTransform>())
                .Where(r => r is not null && r.parent == sliderTemplateRect.parent)
                .Select(r => r.anchoredPosition.y)
                .ToList(),
            sliderSpacing);
        var labelFirstRowY = ChooseInsertionY(
            texts
                .Where(IsSettingsRowLabel)
                .Select(t => t.GetComponent<RectTransform>())
                .Where(r => r is not null && r.parent == labelTemplateRect.parent)
                .Select(r => r.anchoredPosition.y)
                .ToList(),
            labelSpacing);

        if (labelTemplateRect.parent == sliderTemplateRect.parent)
        {
            sliderFirstRowY = labelFirstRowY;
        }

        return new SettingsTarget(
            sliderTemplate,
            labelTemplate,
            sliderTemplateRect.parent,
            labelTemplateRect.parent,
            sliderTemplateRect.anchoredPosition.x,
            sliderFirstRowY,
            sliderSpacing,
            labelTemplateRect.anchoredPosition.x,
            labelFirstRowY,
            labelSpacing);
    }

    private static List<Slider> FindActiveGameSliders()
    {
        var result = new List<Slider>();
        Slider[] sliders;
        try
        {
            sliders = Resources.FindObjectsOfTypeAll<Slider>();
        }
        catch
        {
            return result;
        }

        for (var i = 0; i < sliders.Length; i++)
        {
            var slider = sliders[i];
            if (slider is null ||
                slider.gameObject is null ||
                !slider.gameObject.activeInHierarchy ||
                IsLiteObject(slider.transform))
            {
                continue;
            }

            if (IsInSettingsContext(slider.transform))
            {
                result.Add(slider);
            }
        }

        return result;
    }

    private static List<Text> FindActiveGameTexts()
    {
        var result = new List<Text>();
        Text[] texts;
        try
        {
            texts = Resources.FindObjectsOfTypeAll<Text>();
        }
        catch
        {
            return result;
        }

        for (var i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            if (text is null ||
                text.gameObject is null ||
                !text.gameObject.activeInHierarchy ||
                IsLiteObject(text.transform) ||
                string.IsNullOrWhiteSpace(text.text))
            {
                continue;
            }

            if (IsInSettingsContext(text.transform))
            {
                result.Add(text);
            }
        }

        return result;
    }

    private static Text? FindLabelTemplate(IReadOnlyList<Text> texts, RectTransform sliderTemplateRect)
    {
        foreach (var preferred in PreferredAnchorLabels)
        {
            var match = texts.FirstOrDefault(t => string.Equals(t.text.Trim(), preferred, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        var sliderY = sliderTemplateRect.position.y;
        return texts
            .Where(IsSettingsRowLabel)
            .OrderBy(t => Math.Abs(t.GetComponent<RectTransform>().position.y - sliderY))
            .FirstOrDefault();
    }

    private static bool IsSettingsRowLabel(Text text)
    {
        var value = text.text.Trim();
        return PreferredAnchorLabels.Any(label => string.Equals(value, label, StringComparison.OrdinalIgnoreCase)) ||
            value.Contains("volume", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("mode", StringComparison.OrdinalIgnoreCase);
    }

    private static int ScoreSlider(Slider slider)
    {
        var score = 0;
        var chain = BuildNameChain(slider.transform);
        if (ContainsAnyHint(chain))
        {
            score += 100;
        }

        var texts = slider.GetComponentsInParent<RectTransform>(true)
            .SelectMany(r =>
            {
                try
                {
                    return r.GetComponentsInChildren<Text>(true);
                }
                catch
                {
                    return Array.Empty<Text>();
                }
            })
            .Where(t => t is not null && t.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(t.text))
            .Take(40);
        foreach (var text in texts)
        {
            if (ContainsAnyHint(text.text) || IsSettingsRowLabel(text))
            {
                score += 20;
            }
        }

        return score;
    }

    private static bool IsInSettingsContext(Transform transform)
    {
        if (ContainsAnyHint(BuildNameChain(transform)))
        {
            return true;
        }

        var current = transform;
        for (var depth = 0; current is not null && depth < 8; depth++)
        {
            Text[] texts;
            try
            {
                texts = current.GetComponentsInChildren<Text>(true);
            }
            catch
            {
                current = current.parent;
                continue;
            }

            var usefulTextCount = 0;
            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text is null || !text.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(text.text))
                {
                    continue;
                }

                usefulTextCount++;
                if (ContainsAnyHint(text.text) || IsSettingsRowLabel(text))
                {
                    return true;
                }
            }

            if (usefulTextCount > 20)
            {
                break;
            }

            current = current.parent;
        }

        return false;
    }

    private static string BuildNameChain(Transform transform)
    {
        var names = new List<string>();
        var current = transform;
        while (current is not null && names.Count < 10)
        {
            names.Add(current.name ?? string.Empty);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private static bool ContainsAnyHint(string value)
    {
        for (var i = 0; i < SettingsHints.Length; i++)
        {
            if (value.Contains(SettingsHints[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLiteObject(Transform transform)
    {
        var current = transform;
        while (current is not null)
        {
            var name = current.name ?? string.Empty;
            if (name.StartsWith("CrabtyaLite.", StringComparison.Ordinal))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static float EstimateSpacing(IReadOnlyList<float> yPositions, float fallback)
    {
        var ordered = yPositions
            .Distinct()
            .OrderByDescending(v => v)
            .ToList();
        var deltas = new List<float>();
        for (var i = 1; i < ordered.Count; i++)
        {
            var delta = Math.Abs(ordered[i - 1] - ordered[i]);
            if (delta > 4f && delta < 180f)
            {
                deltas.Add(delta);
            }
        }

        if (deltas.Count == 0)
        {
            return fallback;
        }

        deltas.Sort();
        return Math.Max(36f, deltas[deltas.Count / 2]);
    }

    private static float ChooseInsertionY(IReadOnlyList<float> yPositions, float spacing)
    {
        if (yPositions.Count == 0)
        {
            return -spacing;
        }

        return yPositions.Min() - spacing;
    }

    private static void CreateRows(SettingsTarget target)
    {
        _sliderParent = target.SliderParent;
        _labelParent = target.LabelParent;

        var scaleRow = CreateSliderRow(
            target,
            rowIndex: 0,
            labelText: "FOV",
            labelObjectName: ScaleLabelObjectName,
            sliderObjectName: ScaleSliderObjectName,
            valueObjectName: ScaleValueObjectName,
            min: LiteCameraApplicator.MinScale,
            max: LiteCameraApplicator.MaxScale,
            wholeNumbers: false,
            onChanged: OnScaleSliderChanged);
        _scaleSlider = scaleRow.Slider;
        _scaleLabel = scaleRow.Label;
        _scaleValue = scaleRow.Value;

        var invertRow = CreateSliderRow(
            target,
            rowIndex: 1,
            labelText: "Invert Scroll",
            labelObjectName: InvertLabelObjectName,
            sliderObjectName: InvertSliderObjectName,
            valueObjectName: InvertValueObjectName,
            min: 0f,
            max: 1f,
            wholeNumbers: true,
            onChanged: OnInvertSliderChanged);
        _invertSlider = invertRow.Slider;
        _invertLabel = invertRow.Label;
        _invertValue = invertRow.Value;

        var zoomRow = CreateSliderRow(
            target,
            rowIndex: 2,
            labelText: "Scroll Zoom",
            labelObjectName: ZoomLabelObjectName,
            sliderObjectName: ZoomSliderObjectName,
            valueObjectName: ZoomValueObjectName,
            min: 0f,
            max: 1f,
            wholeNumbers: true,
            onChanged: OnZoomSliderChanged);
        _zoomSlider = zoomRow.Slider;
        _zoomLabel = zoomRow.Label;
        _zoomValue = zoomRow.Value;

        _log?.LogInfo(
            $"Crabtya Lite native settings rows injected. SliderParent={target.SliderParent.name}, LabelParent={target.LabelParent.name}");
    }

    private static (Slider Slider, Text Label, Text Value) CreateSliderRow(
        SettingsTarget target,
        int rowIndex,
        string labelText,
        string labelObjectName,
        string sliderObjectName,
        string valueObjectName,
        float min,
        float max,
        bool wholeNumbers,
        Action<float> onChanged)
    {
        var labelObject = UnityEngine.Object.Instantiate(target.LabelTemplate.gameObject, target.LabelParent);
        labelObject.name = labelObjectName;
        var label = labelObject.GetComponent<Text>();
        label.text = labelText;
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchoredPosition = new Vector2(target.LabelX, target.LabelFirstRowY - rowIndex * target.LabelSpacing);

        var sliderObject = UnityEngine.Object.Instantiate(target.SliderTemplate.gameObject, target.SliderParent);
        sliderObject.name = sliderObjectName;
        var slider = sliderObject.GetComponent<Slider>();
        slider.onValueChanged.RemoveAllListeners();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;
        var sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchoredPosition = new Vector2(target.SliderX, target.SliderFirstRowY - rowIndex * target.SliderSpacing);

        var valueObject = UnityEngine.Object.Instantiate(target.LabelTemplate.gameObject, target.LabelParent);
        valueObject.name = valueObjectName;
        var value = valueObject.GetComponent<Text>();
        value.alignment = TextAnchor.MiddleRight;
        var valueRect = valueObject.GetComponent<RectTransform>();
        valueRect.anchoredPosition = new Vector2(target.LabelX + 260f, target.LabelFirstRowY - rowIndex * target.LabelSpacing);
        valueRect.sizeDelta = new Vector2(120f, valueRect.sizeDelta.y);

        slider.onValueChanged.AddListener((System.Action<float>)onChanged);
        return (slider, label, value);
    }

    private static void SyncRows()
    {
        if (_scaleSlider is null || _invertSlider is null || _zoomSlider is null)
        {
            return;
        }

        _suppressChange = true;

        _scaleSlider.minValue = LiteCameraApplicator.MinScale;
        _scaleSlider.maxValue = LiteCameraApplicator.MaxScale;
        _scaleSlider.wholeNumbers = false;
        _scaleSlider.SetValueWithoutNotify(LiteSettingsStore.GetCameraScale());

        _invertSlider.minValue = 0f;
        _invertSlider.maxValue = 1f;
        _invertSlider.wholeNumbers = true;
        _invertSlider.SetValueWithoutNotify(LiteSettingsStore.GetInvertScroll() ? 1f : 0f);

        _zoomSlider.minValue = 0f;
        _zoomSlider.maxValue = 1f;
        _zoomSlider.wholeNumbers = true;
        _zoomSlider.SetValueWithoutNotify(LiteSettingsStore.GetScrollZoomEnabled() ? 1f : 0f);

        _suppressChange = false;

        SetScaleValueText(LiteSettingsStore.GetCameraScale());
        SetInvertValueText(LiteSettingsStore.GetInvertScroll());
        SetZoomValueText(LiteSettingsStore.GetScrollZoomEnabled());
    }

    private static void OnScaleSliderChanged(float value)
    {
        if (_suppressChange)
        {
            return;
        }

        var quantized = Math.Clamp(Quantize(value, LiteCameraApplicator.StepScale), LiteCameraApplicator.MinScale, LiteCameraApplicator.MaxScale);
        LiteSettingsStore.SetCameraScale(quantized);
        LiteCameraApplicator.ApplyToLiveCameras();

        _suppressChange = true;
        _scaleSlider?.SetValueWithoutNotify(quantized);
        _suppressChange = false;

        SetScaleValueText(quantized);
    }

    private static void OnInvertSliderChanged(float value)
    {
        if (_suppressChange)
        {
            return;
        }

        var enabled = value >= 0.5f;
        LiteSettingsStore.SetInvertScroll(enabled);

        _suppressChange = true;
        _invertSlider?.SetValueWithoutNotify(enabled ? 1f : 0f);
        _suppressChange = false;

        SetInvertValueText(enabled);
    }

    private static void OnZoomSliderChanged(float value)
    {
        if (_suppressChange)
        {
            return;
        }

        var enabled = value >= 0.5f;
        LiteSettingsStore.SetScrollZoomEnabled(enabled);

        _suppressChange = true;
        _zoomSlider?.SetValueWithoutNotify(enabled ? 1f : 0f);
        _suppressChange = false;

        SetZoomValueText(enabled);
    }

    private static void SetScaleValueText(float value)
    {
        if (_scaleValue is not null)
        {
            _scaleValue.text = value.ToString("0.##", CultureInfo.InvariantCulture) + "x";
        }
    }

    private static void SetInvertValueText(bool enabled)
    {
        if (_invertValue is not null)
        {
            _invertValue.text = enabled ? "On" : "Off";
        }
    }

    private static void SetZoomValueText(bool enabled)
    {
        if (_zoomValue is not null)
        {
            _zoomValue.text = enabled ? "On" : "Off";
        }
    }

    private static float Quantize(float value, float step)
    {
        return step <= 0f ? value : (float)Math.Round(value / step) * step;
    }

    private static void DestroyInjectedControls()
    {
        DestroyIfPresent(_scaleSlider);
        DestroyIfPresent(_scaleLabel);
        DestroyIfPresent(_scaleValue);

        DestroyIfPresent(_invertSlider);
        DestroyIfPresent(_invertLabel);
        DestroyIfPresent(_invertValue);

        DestroyIfPresent(_zoomSlider);
        DestroyIfPresent(_zoomLabel);
        DestroyIfPresent(_zoomValue);

        _scaleSlider = null;
        _scaleLabel = null;
        _scaleValue = null;

        _invertSlider = null;
        _invertLabel = null;
        _invertValue = null;

        _zoomSlider = null;
        _zoomLabel = null;
        _zoomValue = null;

        _sliderParent = null;
        _labelParent = null;
    }

    private static void DestroyIfPresent(Component? component)
    {
        if (component is not null && component.gameObject is not null)
        {
            UnityEngine.Object.Destroy(component.gameObject);
        }
    }

    private sealed class SettingsTarget
    {
        public SettingsTarget(
            Slider sliderTemplate,
            Text labelTemplate,
            Transform sliderParent,
            Transform labelParent,
            float sliderX,
            float sliderFirstRowY,
            float sliderSpacing,
            float labelX,
            float labelFirstRowY,
            float labelSpacing)
        {
            SliderTemplate = sliderTemplate;
            LabelTemplate = labelTemplate;
            SliderParent = sliderParent;
            LabelParent = labelParent;
            SliderX = sliderX;
            SliderFirstRowY = sliderFirstRowY;
            SliderSpacing = sliderSpacing;
            LabelX = labelX;
            LabelFirstRowY = labelFirstRowY;
            LabelSpacing = labelSpacing;
        }

        public Slider SliderTemplate { get; }

        public Text LabelTemplate { get; }

        public Transform SliderParent { get; }

        public Transform LabelParent { get; }

        public float SliderX { get; }

        public float SliderFirstRowY { get; }

        public float SliderSpacing { get; }

        public float LabelX { get; }

        public float LabelFirstRowY { get; }

        public float LabelSpacing { get; }
    }
}
